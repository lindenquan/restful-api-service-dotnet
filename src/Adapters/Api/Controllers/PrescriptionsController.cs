using Application.DTOs;
using Application.Prescriptions.Operations;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Adapters.Api.Controllers;

/// <summary>
/// Prescriptions API controller.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PrescriptionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PrescriptionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get a prescription by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PrescriptionDto>> GetById(int id, CancellationToken ct)
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
    public async Task<ActionResult<PrescriptionDto>> Create(CreatePrescriptionDto dto, CancellationToken ct)
    {
        try
        {
            var command = new CreatePrescriptionCommand(
                dto.PatientId,
                dto.MedicationName,
                dto.Dosage,
                dto.Frequency,
                dto.Quantity,
                dto.RefillsAllowed,
                dto.PrescriberName,
                dto.ExpiryDate,
                dto.Instructions
            );
            var prescription = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = prescription.Id }, prescription);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

