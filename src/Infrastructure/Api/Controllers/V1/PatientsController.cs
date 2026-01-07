using Application.Patients.Operations;
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
/// Request DTO for creating a patient.
/// </summary>
public record CreatePatientRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime DateOfBirth
);

/// <summary>
/// V1 Patients API controller.
/// Returns domain entities directly (serialized to JSON).
/// Supports OData-style pagination: $top, $skip, $count, $orderby.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
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
    /// Supports OData query parameters: $top, $skip, $count, $orderby.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<PagedResult<Patient>>> GetAll(
        [FromQuery] ODataQueryParams query,
        CancellationToken ct)
    {
        var orderByParsed = query.ParseOrderBy();
        var pagedData = await _mediator.Send(new GetPatientsPagedQuery(
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
    /// Get a patient by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.CanRead)]
    public async Task<ActionResult<Patient>> GetById(Guid id, CancellationToken ct)
    {
        var patient = await _mediator.Send(new GetPatientByIdQuery(id), ct);
        if (patient == null)
            return NotFound();
        return Ok(patient);
    }

    /// <summary>
    /// Create a new patient.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.CanCreate)]
    public async Task<ActionResult<Patient>> Create(CreatePatientRequest request)
    {
        try
        {
            var command = new CreatePatientCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                request.DateOfBirth
            );
            // Use CancellationToken.None for writes - let operation complete even if client disconnects
            var patient = await _mediator.Send(command, CancellationToken.None);
            return CreatedAtAction(nameof(GetById), new { id = patient.Id, version = "1.0" }, patient);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

