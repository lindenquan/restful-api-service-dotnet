using Application.Orders.Operations;
using Asp.Versioning;
using Domain;
using DTOs.Shared;
using DTOs.V2;
using Infrastructure.Api.Authorization;
using Infrastructure.Api.Controllers.V2.Mappers;
using Infrastructure.Api.Helpers;
using Infrastructure.Api.Services;
using Infrastructure.Cache;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Api.Controllers.V2;

/// <summary>
/// V2 Orders API Controller.
/// Uses more descriptive DTO naming: "PrescriptionOrder".
/// Includes additional fields like dosage, fulfillment tracking, audit fields.
/// Supports OData-style pagination: $top, $skip, $count, $orderby.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/orders")]
[Authorize] // Requires authentication for all endpoints
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly PaginationSettings _paginationSettings;

    public OrdersController(
        IMediator mediator,
        ICurrentUserService currentUser,
        PaginationSettings paginationSettings)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _paginationSettings = paginationSettings;
    }

    /// <summary>
    /// Get prescription orders with pagination.
    /// Supports OData query parameters: $top, $skip, $count, $orderby.
    /// </summary>
    /// <param name="query">OData query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged result with @odata.context, @odata.count and @odata.nextLink.</returns>
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PagedResult<PrescriptionOrderDto>>> GetAll(
        [FromQuery] ODataQueryOptions query,
        CancellationToken ct)
    {
        var primarySort = query.GetPrimarySortField();
        var pagedData = await _mediator.Send(new GetOrdersPagedQuery(
            query.EffectiveSkip,
            query.GetEffectiveTop(_paginationSettings),
            query.GetEffectiveCount(_paginationSettings),
            primarySort?.Field,
            primarySort?.Descending ?? false), ct);

        var result = PaginationHelper.BuildPagedResult(
            pagedData,
            PrescriptionOrderMapper.ToV2Dto,
            Request,
            query,
            _paginationSettings,
            "Orders");

        return Ok(result);
    }

    /// <summary>
    /// Get prescription order by ID.
    /// DEMO: Uses RemoteCache with Serializable consistency - readers wait during writes.
    /// Use for critical data where stale reads are unacceptable.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    [RemoteCache(CacheConsistency.Serializable, TtlSeconds = 60, KeyPrefix = "order")] // Serializable, 1-min TTL
    public async Task<ActionResult<PrescriptionOrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var order = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (order == null)
            return NotFound();
        return Ok(PrescriptionOrderMapper.ToV2Dto(order));
    }

    /// <summary>
    /// Get prescription orders by patient ID with pagination.
    /// Supports OData query parameters: $top, $skip, $count, $orderby.
    /// </summary>
    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PagedResult<PrescriptionOrderDto>>> GetByPatient(
        Guid patientId,
        [FromQuery] ODataQueryOptions query,
        CancellationToken ct)
    {
        var primarySort = query.GetPrimarySortField();
        var pagedData = await _mediator.Send(new GetOrdersByPatientPagedQuery(
            patientId,
            query.EffectiveSkip,
            query.GetEffectiveTop(_paginationSettings),
            query.GetEffectiveCount(_paginationSettings),
            primarySort?.Field,
            primarySort?.Descending ?? false), ct);

        var result = PaginationHelper.BuildPagedResult(
            pagedData,
            PrescriptionOrderMapper.ToV2Dto,
            Request,
            query,
            _paginationSettings,
            "Orders");

        return Ok(result);
    }

    /// <summary>
    /// Get prescription orders by status.
    /// DEMO: Uses RemoteCache with default (Eventual) consistency and no custom TTL.
    /// </summary>
    [HttpGet("status/{status}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    [RemoteCache(KeyPrefix = "orders-by-status")] // Eventual consistency (default), default TTL from config
    public async Task<ActionResult<IEnumerable<PrescriptionOrderDto>>> GetByStatus(string status, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            return BadRequest($"Invalid status: {status}");

        var orders = await _mediator.Send(new GetOrdersByStatusQuery(orderStatus), ct);
        return Ok(PrescriptionOrderMapper.ToV2Dtos(orders));
    }

    /// <summary>
    /// Create a new prescription order.
    /// V2 supports IsUrgent flag.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreate)]
    public async Task<ActionResult<PrescriptionOrderDto>> Create(CreatePrescriptionOrderRequest request)
    {
        // Mapper creates Command directly - no intermediate DTO
        var command = PrescriptionOrderMapper.ToCommand(request, _currentUser.UserId);
        // Use CancellationToken.None for writes - let operation complete even if client disconnects
        var order = await _mediator.Send(command, CancellationToken.None);
        var v2Dto = PrescriptionOrderMapper.ToV2Dto(order);
        return CreatedAtAction(nameof(GetById), new { id = v2Dto.Id, version = "2.0" }, v2Dto);
    }

    /// <summary>
    /// Update prescription order status.
    /// V2 supports updating both status and notes.
    /// Admin only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanUpdate)]
    public async Task<ActionResult<PrescriptionOrderDto>> Update(Guid id, UpdatePrescriptionOrderRequest request)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status))
            return BadRequest($"Invalid status: {request.Status}");

        // Mapper creates Command directly - no intermediate DTO
        var command = PrescriptionOrderMapper.ToCommand(id, request, status, _currentUser.UserId);
        // Use CancellationToken.None for writes - let operation complete even if client disconnects
        var order = await _mediator.Send(command, CancellationToken.None);

        if (order == null)
            return NotFound();
        return Ok(PrescriptionOrderMapper.ToV2Dto(order));
    }

    /// <summary>
    /// Delete a prescription order.
    /// Regular users: soft delete only.
    /// Admin users: can optionally hard delete with ?permanent=true.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanDelete)]
    public async Task<ActionResult> Delete(Guid id, [FromQuery] bool permanent = false)
    {
        // Only admin can hard delete
        var hardDelete = permanent && _currentUser.IsAdmin;

        var command = new DeleteOrderCommand(id, hardDelete, _currentUser.UserId);
        // Use CancellationToken.None for writes - let operation complete even if client disconnects
        var result = await _mediator.Send(command, CancellationToken.None);
        if (!result)
            return NotFound();
        return NoContent();
    }
}

