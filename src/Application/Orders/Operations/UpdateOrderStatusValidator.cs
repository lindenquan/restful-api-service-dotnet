using FluentValidation;

namespace Application.Orders.Operations;

/// <summary>
/// Validator for UpdateOrderStatusCommand.
/// </summary>
public class UpdateOrderStatusValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0)
            .WithMessage("OrderId must be a positive number");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Invalid order status");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null);
    }
}

