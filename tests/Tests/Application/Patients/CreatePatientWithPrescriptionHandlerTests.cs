using Application.Interfaces.Repositories;
using Application.Patients.Operations;
using Domain;
using Moq;
using Shouldly;

namespace Tests.Application.Patients;

/// <summary>
/// Unit tests for CreatePatientWithPrescriptionHandler.
/// Tests transaction-based patient and prescription creation.
/// </summary>
public class CreatePatientWithPrescriptionHandlerTests
{
    private static readonly Guid CreatedById = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPatientRepository> _patientRepoMock;
    private readonly Mock<IPrescriptionRepository> _prescriptionRepoMock;
    private readonly CreatePatientWithPrescriptionHandler _handler;

    public CreatePatientWithPrescriptionHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _patientRepoMock = new Mock<IPatientRepository>();
        _prescriptionRepoMock = new Mock<IPrescriptionRepository>();

        _unitOfWorkMock.Setup(u => u.Patients).Returns(_patientRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Prescriptions).Returns(_prescriptionRepoMock.Object);

        _handler = new CreatePatientWithPrescriptionHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreatePatientAndPrescriptionInTransaction()
    {
        // Arrange
        _patientRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        Patient? capturedPatient = null;
        _patientRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .Callback<Patient, CancellationToken>((p, _) => capturedPatient = p)
            .Returns(Task.CompletedTask);

        Prescription? capturedPrescription = null;
        _prescriptionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Prescription>(), It.IsAny<CancellationToken>()))
            .Callback<Prescription, CancellationToken>((p, _) => capturedPrescription = p)
            .Returns(Task.CompletedTask);

        var command = new CreatePatientWithPrescriptionCommand(
            FirstName: "John",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 1, 15),
            Email: "john.doe@example.com",
            Phone: "555-1234",
            MedicationName: "Aspirin",
            Dosage: "100mg",
            Frequency: "Daily",
            Quantity: 30,
            RefillsAllowed: 3,
            PrescriberName: "Dr. Smith",
            ExpiryDate: DateTime.UtcNow.AddYears(1),
            Instructions: "Take with food",
            CreatedBy: CreatedById);

        // Act
        var (patient, prescription) = await _handler.Handle(command, CancellationToken.None);

        // Assert - Patient created correctly
        capturedPatient.ShouldNotBeNull();
        capturedPatient!.FirstName.ShouldBe("John");
        capturedPatient.LastName.ShouldBe("Doe");
        capturedPatient.Email.ShouldBe("john.doe@example.com");
        capturedPatient.CreatedBy.ShouldBe(CreatedById);

        // Assert - Prescription created correctly
        capturedPrescription.ShouldNotBeNull();
        capturedPrescription!.MedicationName.ShouldBe("Aspirin");
        capturedPrescription.Dosage.ShouldBe("100mg");
        capturedPrescription.RefillsRemaining.ShouldBe(3);
        capturedPrescription.PrescriberName.ShouldBe("Dr. Smith");

        // Assert - Transaction was used
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldThrowWithoutStartingTransaction()
    {
        // Arrange
        var existingPatient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "Existing",
            LastName = "Patient",
            Email = "john.doe@example.com"
        };

        _patientRepoMock
            .Setup(r => r.GetByEmailAsync("john.doe@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatient);

        var command = new CreatePatientWithPrescriptionCommand(
            FirstName: "John",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 1, 15),
            Email: "john.doe@example.com",
            Phone: null,
            MedicationName: "Aspirin",
            Dosage: "100mg",
            Frequency: "Daily",
            Quantity: 30,
            RefillsAllowed: 3,
            PrescriberName: "Dr. Smith",
            ExpiryDate: DateTime.UtcNow.AddYears(1),
            Instructions: null);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain("already exists");

        // Should not have started a transaction for validation errors
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPatientAddFails_ShouldRollbackTransaction()
    {
        // Arrange
        _patientRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        _patientRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var command = new CreatePatientWithPrescriptionCommand(
            FirstName: "John",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 1, 15),
            Email: "john.doe@example.com",
            Phone: null,
            MedicationName: "Aspirin",
            Dosage: "100mg",
            Frequency: "Daily",
            Quantity: 30,
            RefillsAllowed: 3,
            PrescriberName: "Dr. Smith",
            ExpiryDate: DateTime.UtcNow.AddYears(1),
            Instructions: null);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Transaction should have been started and rolled back
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPrescriptionAddFails_ShouldRollbackTransaction()
    {
        // Arrange
        _patientRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        _patientRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _prescriptionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Prescription>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var command = new CreatePatientWithPrescriptionCommand(
            FirstName: "John",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 1, 15),
            Email: "john.doe@example.com",
            Phone: null,
            MedicationName: "Aspirin",
            Dosage: "100mg",
            Frequency: "Daily",
            Quantity: 30,
            RefillsAllowed: 3,
            PrescriberName: "Dr. Smith",
            ExpiryDate: DateTime.UtcNow.AddYears(1),
            Instructions: null);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Patient was added successfully
        _patientRepoMock.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Once);

        // Transaction should rollback - patient add should be undone
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldLinkPrescriptionToPatient()
    {
        // Arrange
        _patientRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        Guid capturedPatientId = Guid.Empty;
        _patientRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .Callback<Patient, CancellationToken>((p, _) =>
            {
                // Simulate ID generation that happens in repository
                if (p.Id == Guid.Empty)
                    p.Id = Guid.NewGuid();
                capturedPatientId = p.Id;
            })
            .Returns(Task.CompletedTask);

        Prescription? capturedPrescription = null;
        _prescriptionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Prescription>(), It.IsAny<CancellationToken>()))
            .Callback<Prescription, CancellationToken>((p, _) => capturedPrescription = p)
            .Returns(Task.CompletedTask);

        var command = new CreatePatientWithPrescriptionCommand(
            FirstName: "John",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 1, 15),
            Email: "john.doe@example.com",
            Phone: null,
            MedicationName: "Aspirin",
            Dosage: "100mg",
            Frequency: "Daily",
            Quantity: 30,
            RefillsAllowed: 3,
            PrescriberName: "Dr. Smith",
            ExpiryDate: DateTime.UtcNow.AddYears(1),
            Instructions: null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Prescription should reference the created patient
        capturedPrescription.ShouldNotBeNull();
        capturedPrescription!.PatientId.ShouldBe(capturedPatientId);
    }
}

