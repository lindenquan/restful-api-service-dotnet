using FluentValidation;

namespace Application.Orders.Operations;

/// <summary>
/// Validator for CreateOrderCommand.
/// </summary>
public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.PatientId)
            .GreaterThan(0)
            .WithMessage("PatientId must be a positive number");

        RuleFor(x => x.PrescriptionId)
            .GreaterThan(0)
            .WithMessage("PrescriptionId must be a positive number");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null);
    }
}

