using Application.Interfaces.Repositories;
using Application.Prescriptions.Shared;
using Entities;
using MediatR;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Command to create a new prescription.
/// Uses internal DTO - controllers map to/from versioned DTOs.
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
) : IRequest<InternalPrescriptionDto>;

/// <summary>
/// Handler for CreatePrescriptionCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreatePrescriptionHandler : IRequestHandler<CreatePrescriptionCommand, InternalPrescriptionDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrescriptionHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<InternalPrescriptionDto> Handle(CreatePrescriptionCommand request, CancellationToken ct)
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

        // Reload with patient details for mapping
        var savedPrescription = await _unitOfWork.Prescriptions.GetByIdWithPatientAsync(prescription.Id, ct);
        return EntityToInternalDto.Map(savedPrescription!);
    }
}

