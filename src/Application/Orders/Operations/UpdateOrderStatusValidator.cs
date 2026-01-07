using FluentValidation;

namespace Application.Orders.Operations;

/// <summary>
/// Validator for UpdateOrderStatusCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class UpdateOrderStatusValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Invalid order status");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null);
    }
}

