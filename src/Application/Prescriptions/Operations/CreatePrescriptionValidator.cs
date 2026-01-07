using FluentValidation;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Validator for CreatePrescriptionCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreatePrescriptionValidator : AbstractValidator<CreatePrescriptionCommand>
{
    public CreatePrescriptionValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("PatientId is required");

        RuleFor(x => x.MedicationName)
            .NotEmpty()
            .WithMessage("Medication name is required")
            .MaximumLength(200)
            .WithMessage("Medication name cannot exceed 200 characters");

        RuleFor(x => x.Dosage)
            .NotEmpty()
            .WithMessage("Dosage is required")
            .MaximumLength(50)
            .WithMessage("Dosage cannot exceed 50 characters");

        RuleFor(x => x.Frequency)
            .NotEmpty()
            .WithMessage("Frequency is required")
            .MaximumLength(100)
            .WithMessage("Frequency cannot exceed 100 characters");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0")
            .LessThanOrEqualTo(1000)
            .WithMessage("Quantity cannot exceed 1000");

        RuleFor(x => x.RefillsAllowed)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Refills allowed cannot be negative")
            .LessThanOrEqualTo(12)
            .WithMessage("Refills allowed cannot exceed 12");

        RuleFor(x => x.PrescriberName)
            .NotEmpty()
            .WithMessage("Prescriber name is required")
            .MaximumLength(200)
            .WithMessage("Prescriber name cannot exceed 200 characters");

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Expiry date must be in the future")
            .LessThan(DateTime.UtcNow.AddYears(2))
            .WithMessage("Expiry date cannot be more than 2 years in the future");

        RuleFor(x => x.Instructions)
            .MaximumLength(1000)
            .WithMessage("Instructions cannot exceed 1000 characters")
            .When(x => x.Instructions != null);
    }
}

