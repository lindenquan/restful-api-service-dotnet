using Application.Interfaces.Repositories;
using FluentValidation;

namespace Application.Orders.Operations;

/// <summary>
/// Validator for CreateOrderCommand.
/// Includes async database validation for entity existence and business rules.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderValidator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;

        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("PatientId is required")
            .MustAsync(PatientExistsAsync)
            .WithMessage("Patient does not exist");

        RuleFor(x => x.PrescriptionId)
            .NotEmpty()
            .WithMessage("PrescriptionId is required")
            .MustAsync(PrescriptionExistsAsync)
            .WithMessage("Prescription does not exist")
            .MustAsync(PrescriptionNotExpiredAsync)
            .WithMessage("Prescription has expired")
            .MustAsync(PrescriptionHasRefillsAsync)
            .WithMessage("Prescription has no refills remaining");

        // Cross-field validation: prescription must belong to the patient
        RuleFor(x => x)
            .MustAsync(PrescriptionBelongsToPatientAsync)
            .WithMessage("Prescription does not belong to this patient")
            .When(x => x.PatientId != Guid.Empty && x.PrescriptionId != Guid.Empty);

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null);
    }

    private async Task<bool> PatientExistsAsync(Guid patientId, CancellationToken ct)
    {
        // Let NotEmpty handle empty GUIDs
        if (patientId == Guid.Empty)
        {
            return true;
        }

        var patient = await _unitOfWork.Patients.GetByIdAsync(patientId, ct);
        return patient != null;
    }

    private async Task<bool> PrescriptionExistsAsync(Guid prescriptionId, CancellationToken ct)
    {
        // Let NotEmpty handle empty GUIDs
        if (prescriptionId == Guid.Empty)
        {
            return true;
        }

        var prescription = await _unitOfWork.Prescriptions.GetByIdAsync(prescriptionId, ct);
        return prescription != null;
    }

    private async Task<bool> PrescriptionNotExpiredAsync(Guid prescriptionId, CancellationToken ct)
    {
        if (prescriptionId == Guid.Empty)
        {
            return true;
        }

        var prescription = await _unitOfWork.Prescriptions.GetByIdAsync(prescriptionId, ct);
        return prescription == null || !prescription.IsExpired;
    }

    private async Task<bool> PrescriptionHasRefillsAsync(Guid prescriptionId, CancellationToken ct)
    {
        if (prescriptionId == Guid.Empty)
        {
            return true;
        }

        var prescription = await _unitOfWork.Prescriptions.GetByIdAsync(prescriptionId, ct);
        return prescription == null || prescription.RefillsRemaining > 0;
    }

    private async Task<bool> PrescriptionBelongsToPatientAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var prescription = await _unitOfWork.Prescriptions.GetByIdAsync(command.PrescriptionId, ct);
        return prescription == null || prescription.PatientId == command.PatientId;
    }
}

