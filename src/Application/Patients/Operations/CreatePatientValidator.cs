using FluentValidation;

namespace Application.Patients.Operations;

/// <summary>
/// Validator for CreatePatientCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreatePatientValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(100)
            .WithMessage("First name cannot exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MaximumLength(100)
            .WithMessage("Last name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .MaximumLength(200)
            .WithMessage("Email cannot exceed 200 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .WithMessage("Phone cannot exceed 20 characters")
            .When(x => x.Phone != null);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .WithMessage("Date of birth is required")
            .LessThan(DateTime.UtcNow)
            .WithMessage("Date of birth must be in the past")
            .GreaterThan(DateTime.UtcNow.AddYears(-150))
            .WithMessage("Date of birth is invalid");
    }
}

