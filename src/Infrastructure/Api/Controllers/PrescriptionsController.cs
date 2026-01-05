using Application.Prescriptions.Operations;
using Application.Prescriptions.Shared;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Api.Controllers;

/// <summary>
/// Prescriptions API controller.
/// Note: This is a simple non-versioned controller that directly returns internal DTOs.
/// For production, consider creating versioned endpoints like Orders (V1/V2).
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
    public async Task<ActionResult<InternalPrescriptionDto>> GetById(int id, CancellationToken ct)
    {
        var prescription = await _mediator.Send(new GetPrescriptionByIdQuery(id), ct);
        if (prescription == null)
            return NotFound();
        return Ok(prescription);
    }

    /// <summary>
    /// Create a new prescription.
    /// </summary>
    /// <param name="request">Prescription creation request</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost]
    public async Task<ActionResult<InternalPrescriptionDto>> Create(CreatePrescriptionRequest request, CancellationToken ct)
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
            return CreatedAtAction(nameof(GetById), new { id = prescription.Id }, prescription);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

/// <summary>
/// Request DTO for creating a prescription.
/// </summary>
public record CreatePrescriptionRequest(
    int PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions
);

