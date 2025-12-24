using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Entities;
using FluentAssertions;
using Moq;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for CreateOrderHandler.
/// </summary>
public class CreateOrderHandlerTests
{
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

        _handler = new CreateOrderHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateOrderWithPendingStatus()
    {
        // Arrange
        var patient = new Patient { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@test.com" };
        var prescription = new Prescription
        {
            Id = 1,
            PatientId = 1,
            MedicationName = "Aspirin",
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };

        _patientRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(prescription);

        PrescriptionOrder? capturedOrder = null;
        _orderRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PrescriptionOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PrescriptionOrder, CancellationToken>((o, _) => capturedOrder = o)
            .Returns(Task.CompletedTask);

        _orderRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new PrescriptionOrder
            {
                Id = id,
                PatientId = 1,
                Patient = patient,
                PrescriptionId = 1,
                Prescription = prescription,
                Status = OrderStatus.Pending,
                OrderDate = DateTime.UtcNow
            });

        var command = new CreateOrderCommand(PatientId: 1, PrescriptionId: 1, Notes: "Test notes", CreatedBy: "admin");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedOrder.Should().NotBeNull();
        capturedOrder!.Status.Should().Be(OrderStatus.Pending);
        capturedOrder.Notes.Should().Be("Test notes");
        capturedOrder.Metadata.CreatedBy.Should().Be("admin");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentPatient_ShouldThrowArgumentException()
    {
        // Arrange
        _patientRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Patient?)null);

        var command = new CreateOrderCommand(PatientId: 999, PrescriptionId: 1, Notes: null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Patient with ID 999 not found*");
    }

    [Fact]
    public async Task Handle_WithNonExistentPrescription_ShouldThrowArgumentException()
    {
        // Arrange
        var patient = new Patient { Id = 1, FirstName = "John", LastName = "Doe" };
        _patientRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Prescription?)null);

        var command = new CreateOrderCommand(PatientId: 1, PrescriptionId: 999, Notes: null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Prescription with ID 999 not found*");
    }

    [Fact]
    public async Task Handle_WithExpiredPrescription_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var patient = new Patient { Id = 1, FirstName = "John", LastName = "Doe" };
        var expiredPrescription = new Prescription
        {
            Id = 1,
            PatientId = 1,
            MedicationName = "Aspirin",
            ExpiryDate = DateTime.UtcNow.AddDays(-1) // Expired yesterday
        };

        _patientRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(expiredPrescription);

        var command = new CreateOrderCommand(PatientId: 1, PrescriptionId: 1, Notes: null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired prescription*");
    }
}

