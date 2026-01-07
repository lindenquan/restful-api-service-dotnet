using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Command to create a new prescription.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record CreatePrescriptionCommand(
    Guid PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions
) : IRequest<Prescription>;

/// <summary>
/// Handler for CreatePrescriptionCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreatePrescriptionHandler : IRequestHandler<CreatePrescriptionCommand, Prescription>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrescriptionHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Prescription> Handle(CreatePrescriptionCommand request, CancellationToken ct)
    {
        _ = await _unitOfWork.Patients.GetByIdAsync(request.PatientId, ct)
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

        // Reload with patient details
        return (await _unitOfWork.Prescriptions.GetByIdWithPatientAsync(prescription.Id, ct))!;
    }
}

