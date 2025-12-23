using Application.DTOs;
using Application.Interfaces.Repositories;
using Entities;
using MediatR;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Command to create a new prescription.
/// </summary>
public record CreatePrescriptionCommand(
    int PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions
) : IRequest<PrescriptionDto>;

/// <summary>
/// Handler for CreatePrescriptionCommand.
/// </summary>
public class CreatePrescriptionHandler : IRequestHandler<CreatePrescriptionCommand, PrescriptionDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrescriptionHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PrescriptionDto> Handle(CreatePrescriptionCommand request, CancellationToken ct)
    {
        var patient = await _unitOfWork.Patients.GetByIdAsync(request.PatientId, ct)
            ?? throw new ArgumentException($"Patient with ID {request.PatientId} not found");

        var prescription = new Prescription
        {
            PatientId = request.PatientId,
            MedicationName = request.MedicationName,
            Dosage = request.Dosage,
            Frequency = request.Frequency,
            Quantity = request.Quantity,
            RefillsRemaining = request.RefillsAllowed,
            PrescriberName = request.PrescriberName,
            PrescribedDate = DateTime.UtcNow,
            ExpiryDate = request.ExpiryDate,
            Instructions = request.Instructions
        };

        await _unitOfWork.Prescriptions.AddAsync(prescription, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        prescription.Patient = patient;  // Set for DTO mapping
        return MapToDto(prescription);
    }

    private static PrescriptionDto MapToDto(Prescription p) => new(
        p.Id,
        p.PatientId,
        p.Patient?.FullName ?? "Unknown",
        p.MedicationName,
        p.Dosage,
        p.Frequency,
        p.Quantity,
        p.RefillsRemaining,
        p.PrescriberName,
        p.PrescribedDate,
        p.ExpiryDate,
        p.Instructions,
        p.IsExpired,
        p.CanRefill,
        p.CreatedAt
    );
}

