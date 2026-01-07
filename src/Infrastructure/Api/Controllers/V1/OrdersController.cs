using Application.Orders.Operations;
using Asp.Versioning;
using Domain;
using DTOs.Shared;
using DTOs.V1;
using Infrastructure.Api.Authorization;
using Infrastructure.Api.Controllers.V1.Mappers;
using Infrastructure.Api.Helpers;
using Infrastructure.Api.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Api.Controllers.V1;

/// <summary>
/// V1 Orders API Controller.
/// Uses simplified DTO naming: "Order" instead of "PrescriptionOrder".
/// Supports OData-style pagination: $top, $skip, $count, $orderby.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
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
    /// Get all orders with pagination.
    /// Supports OData query parameters: $top, $skip, $count, $orderby.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetAll(
        [FromQuery] ODataQueryParams query,
        CancellationToken ct)
    {
        var orderByParsed = query.ParseOrderBy();
        var pagedData = await _mediator.Send(new GetOrdersPagedQuery(
            query.EffectiveSkip,
            query.GetEffectiveTop(_paginationSettings),
            query.GetEffectiveCount(_paginationSettings),
            orderByParsed?.Property,
            orderByParsed?.Descending ?? false), ct);

        var result = PaginationHelper.BuildPagedResult(
            pagedData,
            OrderMapper.ToV1Dto,
            Request,
            query,
            _paginationSettings);

        return Ok(result);
    }

    /// <summary>
    /// Get order by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var order = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (order == null)
            return NotFound();
        return Ok(OrderMapper.ToV1Dto(order));
    }

    /// <summary>
    /// Get orders by patient ID with pagination.
    /// Supports OData query parameters: $top, $skip, $count, $orderby.
    /// </summary>
    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetByPatient(
        Guid patientId,
        [FromQuery] ODataQueryParams query,
        CancellationToken ct)
    {
        var orderByParsed = query.ParseOrderBy();
        var pagedData = await _mediator.Send(new GetOrdersByPatientPagedQuery(
            patientId,
            query.EffectiveSkip,
            query.GetEffectiveTop(_paginationSettings),
            query.GetEffectiveCount(_paginationSettings),
            orderByParsed?.Property,
            orderByParsed?.Descending ?? false), ct);

        var result = PaginationHelper.BuildPagedResult(
            pagedData,
            OrderMapper.ToV1Dto,
            Request,
            query,
            _paginationSettings);

        return Ok(result);
    }

    /// <summary>
    /// Create a new order.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreate)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request)
    {
        // Mapper creates Command directly - no intermediate DTO
        var command = OrderMapper.ToCommand(request, _currentUser.UserId);
        // Use CancellationToken.None for writes - let operation complete even if client disconnects
        var order = await _mediator.Send(command, CancellationToken.None);
        var v1Dto = OrderMapper.ToV1Dto(order);
        return CreatedAtAction(nameof(GetById), new { id = v1Dto.Id, version = "1.0" }, v1Dto);
    }

    /// <summary>
    /// Update order status (V1 only supports status update, not notes).
    /// Admin only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanUpdate)]
    public async Task<ActionResult<OrderDto>> Update(Guid id, UpdateOrderRequest request)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status))
            return BadRequest($"Invalid status: {request.Status}");

        // Mapper creates Command directly - no intermediate DTO
        var command = OrderMapper.ToCommand(id, request, status, _currentUser.UserId);
        // Use CancellationToken.None for writes - let operation complete even if client disconnects
        var order = await _mediator.Send(command, CancellationToken.None);

        if (order == null)
            return NotFound();
        return Ok(OrderMapper.ToV1Dto(order));
    }

    /// <summary>
    /// Delete an order.
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

