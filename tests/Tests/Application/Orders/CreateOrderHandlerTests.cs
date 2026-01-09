using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using Moq;
using Shouldly;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for CreateOrderHandler.
/// </summary>
public class CreateOrderHandlerTests
{
    private static readonly Guid _patientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _prescriptionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _createdById = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid _nonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPatientRepository> _patientRepoMock;
    private readonly Mock<IPrescriptionRepository> _prescriptionRepoMock;
    private readonly Mock<IPrescriptionOrderRepository> _orderRepoMock;
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _patientRepoMock = new Mock<IPatientRepository>();
        _prescriptionRepoMock = new Mock<IPrescriptionRepository>();
        _orderRepoMock = new Mock<IPrescriptionOrderRepository>();

        _unitOfWorkMock.Setup(u => u.Patients).Returns(_patientRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Prescriptions).Returns(_prescriptionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.PrescriptionOrders).Returns(_orderRepoMock.Object);

        // Mock ExecuteInTransactionAsync to execute the operation directly
        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Guid>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Guid>>, CancellationToken>((operation, _) => operation());

        _handler = new CreateOrderHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateOrderWithPendingStatusAsync()
    {
        // Arrange
        var patient = new Patient { Id = _patientId, FirstName = "John", LastName = "Doe", Email = "john@test.com" };
        var prescription = new Prescription
        {
            Id = _prescriptionId,
            PatientId = _patientId,
            MedicationName = "Aspirin",
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            RefillsRemaining = 3
        };

        _patientRepoMock.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(_prescriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(prescription);

        PrescriptionOrder? capturedOrder = null;
        _orderRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PrescriptionOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PrescriptionOrder, CancellationToken>((o, _) => capturedOrder = o)
            .Returns(Task.CompletedTask);

        _orderRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new PrescriptionOrder
            {
                Id = id,
                PatientId = _patientId,
                Patient = patient,
                PrescriptionId = _prescriptionId,
                Prescription = prescription,
                Status = OrderStatus.Pending,
                OrderDate = DateTime.UtcNow
            });

        var command = new CreateOrderCommand(PatientId: _patientId, PrescriptionId: _prescriptionId, Notes: "Test notes", CreatedBy: _createdById);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedOrder.ShouldNotBeNull();
        capturedOrder!.Status.ShouldBe(OrderStatus.Pending);
        capturedOrder.Notes.ShouldBe("Test notes");
        capturedOrder.CreatedBy.ShouldBe(_createdById);

        // Verify transaction was used
        _unitOfWorkMock.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Guid>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldDecrementRefillsRemainingAsync()
    {
        // Arrange
        var patient = new Patient { Id = _patientId, FirstName = "John", LastName = "Doe", Email = "john@test.com" };
        var prescription = new Prescription
        {
            Id = _prescriptionId,
            PatientId = _patientId,
            MedicationName = "Aspirin",
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            RefillsRemaining = 3
        };

        _patientRepoMock.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(_prescriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(prescription);

        Prescription? capturedPrescription = null;
        _prescriptionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Prescription>(), It.IsAny<CancellationToken>()))
            .Callback<Prescription, CancellationToken>((p, _) => capturedPrescription = p)
            .Returns(Task.CompletedTask);

        _orderRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PrescriptionOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _orderRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrescriptionOrder { Id = Guid.NewGuid(), Status = OrderStatus.Pending });

        var command = new CreateOrderCommand(PatientId: _patientId, PrescriptionId: _prescriptionId, Notes: null, CreatedBy: _createdById);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedPrescription.ShouldNotBeNull();
        capturedPrescription!.RefillsRemaining.ShouldBe(2); // Was 3, decremented to 2
        capturedPrescription.UpdatedBy.ShouldBe(_createdById);
        _prescriptionRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Prescription>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentPatient_ShouldThrowArgumentExceptionAsync()
    {
        // Arrange
        _patientRepoMock.Setup(r => r.GetByIdAsync(_nonExistentId, It.IsAny<CancellationToken>())).ReturnsAsync((Patient?)null);

        var command = new CreateOrderCommand(PatientId: _nonExistentId, PrescriptionId: _prescriptionId, Notes: null);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain($"Patient with ID {_nonExistentId} not found");
    }

    [Fact]
    public async Task Handle_WithNonExistentPrescription_ShouldThrowArgumentExceptionAsync()
    {
        // Arrange
        var patient = new Patient { Id = _patientId, FirstName = "John", LastName = "Doe" };
        _patientRepoMock.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(_nonExistentId, It.IsAny<CancellationToken>())).ReturnsAsync((Prescription?)null);

        var command = new CreateOrderCommand(PatientId: _patientId, PrescriptionId: _nonExistentId, Notes: null);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain($"Prescription with ID {_nonExistentId} not found");
    }

    [Fact]
    public async Task Handle_WithExpiredPrescription_ShouldThrowInvalidOperationExceptionAsync()
    {
        // Arrange
        var patient = new Patient { Id = _patientId, FirstName = "John", LastName = "Doe" };
        var expiredPrescription = new Prescription
        {
            Id = _prescriptionId,
            PatientId = _patientId,
            MedicationName = "Aspirin",
            ExpiryDate = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            RefillsRemaining = 3
        };

        _patientRepoMock.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(_prescriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(expiredPrescription);

        var command = new CreateOrderCommand(PatientId: _patientId, PrescriptionId: _prescriptionId, Notes: null);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain("expired prescription");
    }

    [Fact]
    public async Task Handle_WithNoRefillsRemaining_ShouldThrowInvalidOperationExceptionAsync()
    {
        // Arrange
        var patient = new Patient { Id = _patientId, FirstName = "John", LastName = "Doe" };
        var prescription = new Prescription
        {
            Id = _prescriptionId,
            PatientId = _patientId,
            MedicationName = "Aspirin",
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            RefillsRemaining = 0 // No refills left
        };

        _patientRepoMock.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(_prescriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(prescription);

        var command = new CreateOrderCommand(PatientId: _patientId, PrescriptionId: _prescriptionId, Notes: null);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain("no refills remaining");
    }
}

