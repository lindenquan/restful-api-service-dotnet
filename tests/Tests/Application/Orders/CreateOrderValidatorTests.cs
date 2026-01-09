using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using FluentValidation.TestHelper;
using Moq;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for CreateOrderValidator.
/// </summary>
public class CreateOrderValidatorTests
{
    private static readonly Guid _validPatientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _validPrescriptionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPatientRepository> _patientRepoMock;
    private readonly Mock<IPrescriptionRepository> _prescriptionRepoMock;
    private readonly CreateOrderValidator _validator;

    public CreateOrderValidatorTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _patientRepoMock = new Mock<IPatientRepository>();
        _prescriptionRepoMock = new Mock<IPrescriptionRepository>();

        _unitOfWorkMock.Setup(u => u.Patients).Returns(_patientRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Prescriptions).Returns(_prescriptionRepoMock.Object);

        // Default: entities exist and are valid
        _patientRepoMock
            .Setup(r => r.GetByIdAsync(_validPatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = _validPatientId, FirstName = "Test", LastName = "Patient" });

        _prescriptionRepoMock
            .Setup(r => r.GetByIdAsync(_validPrescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prescription
            {
                Id = _validPrescriptionId,
                PatientId = _validPatientId,
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                RefillsRemaining = 3
            });

        _validator = new CreateOrderValidator(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Validate_ValidCommand_ShouldNotHaveErrorsAsync()
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: _validPatientId, PrescriptionId: _validPrescriptionId, Notes: "Valid notes");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_EmptyPatientId_ShouldHaveErrorAsync()
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: Guid.Empty, PrescriptionId: _validPrescriptionId, Notes: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PatientId)
            .WithErrorMessage("PatientId is required");
    }

    [Fact]
    public async Task Validate_EmptyPrescriptionId_ShouldHaveErrorAsync()
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: _validPatientId, PrescriptionId: Guid.Empty, Notes: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrescriptionId)
            .WithErrorMessage("PrescriptionId is required");
    }

    [Fact]
    public async Task Validate_NotesTooLong_ShouldHaveErrorAsync()
    {
        // Arrange
        var longNotes = new string('a', 501);
        var command = new CreateOrderCommand(PatientId: _validPatientId, PrescriptionId: _validPrescriptionId, Notes: longNotes);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage("Notes cannot exceed 500 characters");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Short notes")]
    public async Task Validate_ValidNotes_ShouldNotHaveErrorAsync(string? notes)
    {
        // Arrange
        var command = new CreateOrderCommand(PatientId: _validPatientId, PrescriptionId: _validPrescriptionId, Notes: notes);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public async Task Validate_NotesExactly500Characters_ShouldNotHaveErrorAsync()
    {
        // Arrange
        var notes = new string('a', 500);
        var command = new CreateOrderCommand(PatientId: _validPatientId, PrescriptionId: _validPrescriptionId, Notes: notes);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public async Task Validate_PatientDoesNotExist_ShouldHaveErrorAsync()
    {
        // Arrange
        var nonExistentPatientId = Guid.NewGuid();
        _patientRepoMock
            .Setup(r => r.GetByIdAsync(nonExistentPatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var command = new CreateOrderCommand(PatientId: nonExistentPatientId, PrescriptionId: _validPrescriptionId, Notes: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PatientId)
            .WithErrorMessage("Patient does not exist");
    }

    [Fact]
    public async Task Validate_PrescriptionDoesNotExist_ShouldHaveErrorAsync()
    {
        // Arrange
        var nonExistentPrescriptionId = Guid.NewGuid();
        _prescriptionRepoMock
            .Setup(r => r.GetByIdAsync(nonExistentPrescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prescription?)null);

        var command = new CreateOrderCommand(PatientId: _validPatientId, PrescriptionId: nonExistentPrescriptionId, Notes: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrescriptionId)
            .WithErrorMessage("Prescription does not exist");
    }

    [Fact]
    public async Task Validate_PrescriptionExpired_ShouldHaveErrorAsync()
    {
        // Arrange
        var expiredPrescriptionId = Guid.NewGuid();
        _prescriptionRepoMock
            .Setup(r => r.GetByIdAsync(expiredPrescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prescription
            {
                Id = expiredPrescriptionId,
                PatientId = _validPatientId,
                ExpiryDate = DateTime.UtcNow.AddDays(-1), // Expired
                RefillsRemaining = 3
            });

        var command = new CreateOrderCommand(PatientId: _validPatientId, PrescriptionId: expiredPrescriptionId, Notes: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrescriptionId)
            .WithErrorMessage("Prescription has expired");
    }

    [Fact]
    public async Task Validate_PrescriptionNoRefills_ShouldHaveErrorAsync()
    {
        // Arrange
        var noRefillsPrescriptionId = Guid.NewGuid();
        _prescriptionRepoMock
            .Setup(r => r.GetByIdAsync(noRefillsPrescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prescription
            {
                Id = noRefillsPrescriptionId,
                PatientId = _validPatientId,
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                RefillsRemaining = 0 // No refills
            });

        var command = new CreateOrderCommand(PatientId: _validPatientId, PrescriptionId: noRefillsPrescriptionId, Notes: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrescriptionId)
            .WithErrorMessage("Prescription has no refills remaining");
    }

    [Fact]
    public async Task Validate_PrescriptionBelongsToDifferentPatient_ShouldHaveErrorAsync()
    {
        // Arrange
        var differentPatientId = Guid.NewGuid();
        var prescriptionForDifferentPatient = Guid.NewGuid();

        _patientRepoMock
            .Setup(r => r.GetByIdAsync(differentPatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = differentPatientId, FirstName = "Other", LastName = "Patient" });

        _prescriptionRepoMock
            .Setup(r => r.GetByIdAsync(prescriptionForDifferentPatient, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prescription
            {
                Id = prescriptionForDifferentPatient,
                PatientId = _validPatientId, // Belongs to a different patient
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                RefillsRemaining = 3
            });

        var command = new CreateOrderCommand(
            PatientId: differentPatientId,
            PrescriptionId: prescriptionForDifferentPatient,
            Notes: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Prescription does not belong to this patient");
    }
}

