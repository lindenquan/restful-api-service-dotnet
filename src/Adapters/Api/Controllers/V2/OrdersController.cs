using Adapters.Api.Authorization;
using Adapters.Api.Services;
using Application.Orders.Operations;
using Application.Orders.V2.DTOs;
using Application.Orders.V2.Mappers;
using Asp.Versioning;
using Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adapters.Api.Controllers.V2;

/// <summary>
/// V2 Orders API Controller.
/// Uses more descriptive DTO naming: "PrescriptionOrder".
/// Includes additional fields like dosage, fulfillment tracking, audit fields.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
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
    /// Get all prescription orders.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<PrescriptionOrderDto>>> GetAll(CancellationToken ct)
    {
        var internalDtos = await _mediator.Send(new GetAllOrdersQuery(), ct);
        return Ok(PrescriptionOrderMapper.ToV2Dtos(internalDtos));
    }

    /// <summary>
    /// Get prescription order by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PrescriptionOrderDto>> GetById(int id, CancellationToken ct)
    {
        var internalDto = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (internalDto == null)
            return NotFound();
        return Ok(PrescriptionOrderMapper.ToV2Dto(internalDto));
    }

    /// <summary>
    /// Get prescription orders by patient ID.
    /// </summary>
    [HttpGet("patient/{patientId:int}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<PrescriptionOrderDto>>> GetByPatient(int patientId, CancellationToken ct)
    {
        var internalDtos = await _mediator.Send(new GetOrdersByPatientQuery(patientId), ct);
        return Ok(PrescriptionOrderMapper.ToV2Dtos(internalDtos));
    }

    /// <summary>
    /// Get prescription orders by status.
    /// </summary>
    [HttpGet("status/{status}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<PrescriptionOrderDto>>> GetByStatus(string status, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            return BadRequest($"Invalid status: {status}");

        var internalDtos = await _mediator.Send(new GetOrdersByStatusQuery(orderStatus), ct);
        return Ok(PrescriptionOrderMapper.ToV2Dtos(internalDtos));
    }

    /// <summary>
    /// Create a new prescription order.
    /// V2 supports IsUrgent flag.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreate)]
    public async Task<ActionResult<PrescriptionOrderDto>> Create(CreatePrescriptionOrderRequest request, CancellationToken ct)
    {
        // Mapper creates Command directly - no intermediate DTO
        var command = PrescriptionOrderMapper.ToCommand(request, _currentUser.UserName);
        var internalDto = await _mediator.Send(command, ct);
        var v2Dto = PrescriptionOrderMapper.ToV2Dto(internalDto);
        return CreatedAtAction(nameof(GetById), new { id = v2Dto.Id, version = "2.0" }, v2Dto);
    }

    /// <summary>
    /// Update prescription order status.
    /// V2 supports updating both status and notes.
    /// Admin only.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PolicyNames.CanUpdate)]
    public async Task<ActionResult<PrescriptionOrderDto>> Update(int id, UpdatePrescriptionOrderRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status))
            return BadRequest($"Invalid status: {request.Status}");

        // Mapper creates Command directly - no intermediate DTO
        var command = PrescriptionOrderMapper.ToCommand(id, request, status, _currentUser.UserName);
        var internalDto = await _mediator.Send(command, ct);

        if (internalDto == null)
            return NotFound();
        return Ok(PrescriptionOrderMapper.ToV2Dto(internalDto));
    }

    /// <summary>
    /// Delete a prescription order.
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

