using Application.Prescriptions.Operations;
using Asp.Versioning;
using DTOs.Shared;
using DTOs.V2;
using Infrastructure.Api.Authorization;
using Infrastructure.Api.Controllers.V2.Mappers;
using Infrastructure.Api.Helpers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Api.Controllers.V2;

/// <summary>
/// V2 Prescriptions API controller.
/// V2 includes additional fields like status indicators, days until expiry, and audit timestamps.
/// Supports OData-style pagination: $top, $skip, $count, $orderby.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
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
    /// V2 includes status indicators and days until expiry.
    /// Supports OData query parameters: $top, $skip, $count, $orderby.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PagedResult<PrescriptionDto>>> GetAll(
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
            PrescriptionMapper.ToV2Dto,
            Request,
            query,
            _paginationSettings);

        return Ok(result);
    }

    /// <summary>
    /// Get a prescription by ID.
    /// V2 includes status indicators and days until expiry.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PrescriptionDto>> GetById(Guid id, CancellationToken ct)
    {
        var prescription = await _mediator.Send(new GetPrescriptionByIdQuery(id), ct);
        if (prescription == null)
            return NotFound();
        return Ok(PrescriptionMapper.ToV2Dto(prescription));
    }

    /// <summary>
    /// Get prescriptions by patient ID.
    /// V2 supports filtering by patient (V1 doesn't have this endpoint).
    /// </summary>
    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<PrescriptionDto>>> GetByPatient(Guid patientId, CancellationToken ct)
    {
        var prescriptions = await _mediator.Send(new GetAllPrescriptionsQuery(), ct);
        var filtered = prescriptions.Where(p => p.PatientId == patientId);
        return Ok(PrescriptionMapper.ToV2Dtos(filtered));
    }

    /// <summary>
    /// Get active (non-expired, refillable) prescriptions.
    /// V2 supports filtering by status (V1 doesn't have this endpoint).
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<PrescriptionDto>>> GetActive(CancellationToken ct)
    {
        var prescriptions = await _mediator.Send(new GetAllPrescriptionsQuery(), ct);
        var active = prescriptions.Where(p => !p.IsExpired && p.CanRefill);
        return Ok(PrescriptionMapper.ToV2Dtos(active));
    }

    /// <summary>
    /// Get expired prescriptions.
    /// V2 supports filtering by status (V1 doesn't have this endpoint).
    /// </summary>
    [HttpGet("expired")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<PrescriptionDto>>> GetExpired(CancellationToken ct)
    {
        var prescriptions = await _mediator.Send(new GetAllPrescriptionsQuery(), ct);
        var expired = prescriptions.Where(p => p.IsExpired);
        return Ok(PrescriptionMapper.ToV2Dtos(expired));
    }

    /// <summary>
    /// Create a new prescription.
    /// V2 supports controlled substance flag.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreate)]
    public async Task<ActionResult<PrescriptionDto>> Create(CreatePrescriptionRequest request)
    {
        try
        {
            var command = PrescriptionMapper.ToCommand(request);
            // Use CancellationToken.None for writes - let operation complete even if client disconnects
            var prescription = await _mediator.Send(command, CancellationToken.None);
            return CreatedAtAction(nameof(GetById), new { id = prescription.Id, version = "2.0" }, PrescriptionMapper.ToV2Dto(prescription));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

