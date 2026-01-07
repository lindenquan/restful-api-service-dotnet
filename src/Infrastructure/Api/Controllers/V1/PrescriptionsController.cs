using Application.Prescriptions.Operations;
using Asp.Versioning;
using Domain;
using DTOs.Shared;
using Infrastructure.Api.Authorization;
using Infrastructure.Api.Helpers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Api.Controllers.V1;

/// <summary>
/// Request DTO for creating a prescription.
/// </summary>
public record CreatePrescriptionRequest(
    Guid PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions
);

/// <summary>
/// V1 Prescriptions API controller.
/// Returns domain entities directly (serialized to JSON).
/// Supports OData-style pagination: $top, $skip, $count, $orderby.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/prescriptions")]
[Authorize]
public class PrescriptionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PaginationSettings _paginationSettings;

    public PrescriptionsController(IMediator mediator, PaginationSettings paginationSettings)
    {
        _mediator = mediator;
        _paginationSettings = paginationSettings;
    }

    /// <summary>
    /// Get all prescriptions with pagination.
    /// Supports OData query parameters: $top, $skip, $count, $orderby.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PagedResult<Prescription>>> GetAll(
        [FromQuery] ODataQueryParams query,
        CancellationToken ct)
    {
        var orderByParsed = query.ParseOrderBy();
        var pagedData = await _mediator.Send(new GetPrescriptionsPagedQuery(
            query.EffectiveSkip,
            query.GetEffectiveTop(_paginationSettings),
            query.GetEffectiveCount(_paginationSettings),
            orderByParsed?.Property,
            orderByParsed?.Descending ?? false), ct);

        var result = PaginationHelper.BuildPagedResult(
            pagedData,
            p => p,  // V1 returns domain entity directly
            Request,
            query,
            _paginationSettings);

        return Ok(result);
    }

    /// <summary>
    /// Get a prescription by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<Prescription>> GetById(Guid id, CancellationToken ct)
    {
        var prescription = await _mediator.Send(new GetPrescriptionByIdQuery(id), ct);
        if (prescription == null)
            return NotFound();
        return Ok(prescription);
    }

    /// <summary>
    /// Create a new prescription.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreate)]
    public async Task<ActionResult<Prescription>> Create(CreatePrescriptionRequest request, CancellationToken ct)
    {
        try
        {
            var command = new CreatePrescriptionCommand(
                request.PatientId,
                request.MedicationName,
                request.Dosage,
                request.Frequency,
                request.Quantity,
                request.RefillsAllowed,
                request.PrescriberName,
                request.ExpiryDate,
                request.Instructions
            );
            var prescription = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = prescription.Id, version = "1.0" }, prescription);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

