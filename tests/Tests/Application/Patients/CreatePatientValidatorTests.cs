using Application.Patients.Operations;
using FluentValidation.TestHelper;

namespace Tests.Application.Patients;

/// <summary>
/// Unit tests for CreatePatientValidator.
/// </summary>
public class CreatePatientValidatorTests
{
    private readonly CreatePatientValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com",
            Phone: "555-1234",
            DateOfBirth: new DateTime(1990, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyFirstName_ShouldHaveError(string? firstName)
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: firstName!,
            LastName: "Doe",
            Email: "test@example.com",
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-30)
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("First name is required");
    }

    [Fact]
    public void Validate_FirstNameTooLong_ShouldHaveError()
    {
        // Arrange
        var longName = new string('a', 101);
        var command = new CreatePatientCommand(
            FirstName: longName,
            LastName: "Doe",
            Email: "test@example.com",
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-30)
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("First name cannot exceed 100 characters");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyLastName_ShouldHaveError(string? lastName)
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: lastName!,
            Email: "test@example.com",
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-30)
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage("Last name is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyEmail_ShouldHaveError(string? email)
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: email!,
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-30)
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@example.com")]
    public void Validate_InvalidEmailFormat_ShouldHaveError(string email)
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: email,
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-30)
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Invalid email format");
    }

    [Fact]
    public void Validate_PhoneTooLong_ShouldHaveError()
    {
        // Arrange
        var longPhone = new string('1', 21);
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "test@example.com",
            Phone: longPhone,
            DateOfBirth: DateTime.UtcNow.AddYears(-30)
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Phone)
            .WithErrorMessage("Phone cannot exceed 20 characters");
    }
}

