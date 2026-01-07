using Application.Orders.Operations;
using FluentValidation.TestHelper;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for CreateOrderValidator.
/// </summary>
public class CreateOrderValidatorTests
{
    private static readonly Guid ValidPatientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ValidPrescriptionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly CreateOrderValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: ValidPatientId, PrescriptionId: ValidPrescriptionId, Notes: "Valid notes");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyPatientId_ShouldHaveError()
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: Guid.Empty, PrescriptionId: ValidPrescriptionId, Notes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PatientId)
            .WithErrorMessage("PatientId is required");
    }

    [Fact]
    public void Validate_EmptyPrescriptionId_ShouldHaveError()
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: ValidPatientId, PrescriptionId: Guid.Empty, Notes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrescriptionId)
            .WithErrorMessage("PrescriptionId is required");
    }

    [Fact]
    public void Validate_NotesTooLong_ShouldHaveError()
    {
        // Arrange
        var longNotes = new string('a', 501);
        var command = new CreateOrderCommand(PatientId: ValidPatientId, PrescriptionId: ValidPrescriptionId, Notes: longNotes);

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
        var command = new CreateOrderCommand(PatientId: ValidPatientId, PrescriptionId: ValidPrescriptionId, Notes: notes);

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
        var command = new CreateOrderCommand(PatientId: ValidPatientId, PrescriptionId: ValidPrescriptionId, Notes: notes);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }
}

