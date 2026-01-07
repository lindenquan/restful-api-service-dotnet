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
    private static readonly Guid PatientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PrescriptionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CreatedById = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid NonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

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
        var patient = new Patient { Id = PatientId, FirstName = "John", LastName = "Doe", Email = "john@test.com" };
        var prescription = new Prescription
        {
            Id = PrescriptionId,
            PatientId = PatientId,
            MedicationName = "Aspirin",
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };

        _patientRepoMock.Setup(r => r.GetByIdAsync(PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(PrescriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(prescription);

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
                PatientId = PatientId,
                Patient = patient,
                PrescriptionId = PrescriptionId,
                Prescription = prescription,
                Status = OrderStatus.Pending,
                OrderDate = DateTime.UtcNow
            });

        var command = new CreateOrderCommand(PatientId: PatientId, PrescriptionId: PrescriptionId, Notes: "Test notes", CreatedBy: CreatedById);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedOrder.ShouldNotBeNull();
        capturedOrder!.Status.ShouldBe(OrderStatus.Pending);
        capturedOrder.Notes.ShouldBe("Test notes");
        capturedOrder.CreatedBy.ShouldBe(CreatedById);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentPatient_ShouldThrowArgumentException()
    {
        // Arrange
        _patientRepoMock.Setup(r => r.GetByIdAsync(NonExistentId, It.IsAny<CancellationToken>())).ReturnsAsync((Patient?)null);

        var command = new CreateOrderCommand(PatientId: NonExistentId, PrescriptionId: PrescriptionId, Notes: null);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain($"Patient with ID {NonExistentId} not found");
    }

    [Fact]
    public async Task Handle_WithNonExistentPrescription_ShouldThrowArgumentException()
    {
        // Arrange
        var patient = new Patient { Id = PatientId, FirstName = "John", LastName = "Doe" };
        _patientRepoMock.Setup(r => r.GetByIdAsync(PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(NonExistentId, It.IsAny<CancellationToken>())).ReturnsAsync((Prescription?)null);

        var command = new CreateOrderCommand(PatientId: PatientId, PrescriptionId: NonExistentId, Notes: null);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain($"Prescription with ID {NonExistentId} not found");
    }

    [Fact]
    public async Task Handle_WithExpiredPrescription_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var patient = new Patient { Id = PatientId, FirstName = "John", LastName = "Doe" };
        var expiredPrescription = new Prescription
        {
            Id = PrescriptionId,
            PatientId = PatientId,
            MedicationName = "Aspirin",
            ExpiryDate = DateTime.UtcNow.AddDays(-1) // Expired yesterday
        };

        _patientRepoMock.Setup(r => r.GetByIdAsync(PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _prescriptionRepoMock.Setup(r => r.GetByIdAsync(PrescriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(expiredPrescription);

        var command = new CreateOrderCommand(PatientId: PatientId, PrescriptionId: PrescriptionId, Notes: null);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain("expired prescription");
    }
}

