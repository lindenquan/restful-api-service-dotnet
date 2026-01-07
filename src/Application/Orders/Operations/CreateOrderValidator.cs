using FluentValidation;

namespace Application.Orders.Operations;

/// <summary>
/// Validator for CreateOrderCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("PatientId is required");

        RuleFor(x => x.PrescriptionId)
            .NotEmpty()
            .WithMessage("PrescriptionId is required");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null);
    }
}

