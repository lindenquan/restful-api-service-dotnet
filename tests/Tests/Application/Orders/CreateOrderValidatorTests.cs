using Application.Orders.Operations;
using FluentValidation.TestHelper;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for CreateOrderValidator.
/// </summary>
public class CreateOrderValidatorTests
{
    private readonly CreateOrderValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: 1, PrescriptionId: 1, Notes: "Valid notes");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidPatientId_ShouldHaveError(int patientId)
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: patientId, PrescriptionId: 1, Notes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PatientId)
            .WithErrorMessage("PatientId must be a positive number");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidPrescriptionId_ShouldHaveError(int prescriptionId)
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: 1, PrescriptionId: prescriptionId, Notes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrescriptionId)
            .WithErrorMessage("PrescriptionId must be a positive number");
    }

    [Fact]
    public void Validate_NotesTooLong_ShouldHaveError()
    {
        // Arrange
        var longNotes = new string('a', 501);
        var command = new CreateOrderCommand(PatientId: 1, PrescriptionId: 1, Notes: longNotes);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage("Notes cannot exceed 500 characters");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Short notes")]
    public void Validate_ValidNotes_ShouldNotHaveError(string? notes)
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: 1, PrescriptionId: 1, Notes: notes);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Validate_NotesExactly500Characters_ShouldNotHaveError()
    {
        // Arrange
        var notes = new string('a', 500);
        var command = new CreateOrderCommand(PatientId: 1, PrescriptionId: 1, Notes: notes);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }
}

