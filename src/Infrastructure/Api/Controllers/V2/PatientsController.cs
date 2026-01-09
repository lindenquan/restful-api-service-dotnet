using Application.Patients.Operations;
using Asp.Versioning;
using DTOs.Shared;
using DTOs.V2;
using Infrastructure.Api.Authorization;
using Infrastructure.Api.Controllers.V2.Mappers;
using Infrastructure.Api.Helpers;
using Infrastructure.Cache;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Api.Controllers.V2;

/// <summary>
/// V2 Patients API controller.
/// V2 includes additional fields like age calculation and audit timestamps.
/// Supports OData query parameters: $top, $skip, $count, $orderby.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/patients")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PaginationSettings _paginationSettings;

    public PatientsController(IMediator mediator, PaginationSettings paginationSettings)
    {
        _mediator = mediator;
        _paginationSettings = paginationSettings;
    }

    /// <summary>
    /// Get all patients with pagination.
    /// V2 includes age calculation and audit fields.
    /// Supports OData query parameters: $top, $skip, $count, $orderby.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PagedResult<PatientDto>>> GetAll(
        [FromQuery] ODataQueryOptions query,
        CancellationToken ct)
    {
        var primarySort = query.GetPrimarySortField();
        var pagedData = await _mediator.Send(new GetPatientsPagedQuery(
            query.EffectiveSkip,
            query.GetEffectiveTop(_paginationSettings),
            query.GetEffectiveCount(_paginationSettings),
            primarySort?.Field,
            primarySort?.Descending ?? false), ct);

        var result = PaginationHelper.BuildPagedResult(
            pagedData,
            PatientMapper.ToV2Dto,
            Request,
            query,
            _paginationSettings,
            "Patients");

        return Ok(result);
    }

    /// <summary>
    /// Get a patient by ID.
    /// V2 includes age calculation and audit fields.
    /// DEMO: Uses RemoteCache with Eventual consistency (default) - good for frequently accessed data.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    [RemoteCache(TtlSeconds = 300, KeyPrefix = "patient")] // 5-minute TTL, Eventual consistency
    public async Task<ActionResult<PatientDto>> GetById(Guid id, CancellationToken ct)
    {
        var patient = await _mediator.Send(new GetPatientByIdQuery(id), ct);
        if (patient == null)
            return NotFound();
        return Ok(PatientMapper.ToV2Dto(patient));
    }

    /// <summary>
    /// Search patients by name.
    /// V2 supports search functionality (V1 doesn't).
    /// </summary>
    [HttpGet("search")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<IEnumerable<PatientDto>>> Search([FromQuery] string? name, CancellationToken ct)
    {
        var patients = await _mediator.Send(new GetAllPatientsQuery(), ct);

        if (!string.IsNullOrWhiteSpace(name))
        {
            patients = patients.Where(p =>
                p.FullName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                p.Email.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(PatientMapper.ToV2Dtos(patients));
    }

    /// <summary>
    /// Create a new patient.
    /// V2 supports additional notes field.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreate)]
    public async Task<ActionResult<PatientDto>> Create(CreatePatientRequest request)
    {
        try
        {
            var command = PatientMapper.ToCommand(request);
            // Use CancellationToken.None for writes - let operation complete even if client disconnects
            var patient = await _mediator.Send(command, CancellationToken.None);
            return CreatedAtAction(nameof(GetById), new { id = patient.Id, version = "2.0" }, PatientMapper.ToV2Dto(patient));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

