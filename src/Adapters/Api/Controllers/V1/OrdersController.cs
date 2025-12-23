using Adapters.Api.Authorization;
using Adapters.Api.Services;
using Application.Orders.Operations;
using Application.Orders.V1.DTOs;
using Application.Orders.V1.Mappers;
using Asp.Versioning;
using Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adapters.Api.Controllers.V1;

/// <summary>
/// V1 Orders API Controller.
/// Uses simplified DTO naming: "Order" instead of "PrescriptionOrder".
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Authorize] // Requires authentication for all endpoints
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public OrdersController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get all orders.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll(CancellationToken ct)
    {
        var internalDtos = await _mediator.Send(new GetAllOrdersQuery(), ct);
        return Ok(OrderMapper.ToV1Dtos(internalDtos));
    }

    /// <summary>
    /// Get order by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct)
    {
        var internalDto = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (internalDto == null)
            return NotFound();
        return Ok(OrderMapper.ToV1Dto(internalDto));
    }

    /// <summary>
    /// Get orders by patient ID.
    /// </summary>
    [HttpGet("patient/{patientId:int}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetByPatient(int patientId, CancellationToken ct)
    {
        var internalDtos = await _mediator.Send(new GetOrdersByPatientQuery(patientId), ct);
        return Ok(OrderMapper.ToV1Dtos(internalDtos));
    }

    /// <summary>
    /// Create a new order.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreate)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        // Mapper creates Command directly - no intermediate DTO
        var command = OrderMapper.ToCommand(request, _currentUser.UserName);
        var internalDto = await _mediator.Send(command, ct);
        var v1Dto = OrderMapper.ToV1Dto(internalDto);
        return CreatedAtAction(nameof(GetById), new { id = v1Dto.Id, version = "1.0" }, v1Dto);
    }

    /// <summary>
    /// Update order status (V1 only supports status update, not notes).
    /// Admin only.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PolicyNames.CanUpdate)]
    public async Task<ActionResult<OrderDto>> Update(int id, UpdateOrderRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status))
            return BadRequest($"Invalid status: {request.Status}");

        // Mapper creates Command directly - no intermediate DTO
        var command = OrderMapper.ToCommand(id, request, status, _currentUser.UserName);
        var internalDto = await _mediator.Send(command, ct);

        if (internalDto == null)
            return NotFound();
        return Ok(OrderMapper.ToV1Dto(internalDto));
    }

    /// <summary>
    /// Delete an order.
    /// Regular users: soft delete only.
    /// Admin users: can optionally hard delete with ?permanent=true.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PolicyNames.CanDelete)]
    public async Task<ActionResult> Delete(int id, [FromQuery] bool permanent = false, CancellationToken ct = default)
    {
        // Only admin can hard delete
        var hardDelete = permanent && _currentUser.IsAdmin;

        var command = new DeleteOrderCommand(id, hardDelete, _currentUser.UserName);
        var result = await _mediator.Send(command, ct);
        if (!result)
            return NotFound();
        return NoContent();
    }
}

