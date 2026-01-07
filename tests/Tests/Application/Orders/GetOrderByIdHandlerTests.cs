using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using Moq;
using Shouldly;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for GetOrderByIdHandler.
/// </summary>
public class GetOrderByIdHandlerTests
{
    private static readonly Guid TestOrderId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid PatientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PrescriptionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid NonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPrescriptionOrderRepository> _orderRepoMock;
    private readonly GetOrderByIdHandler _handler;

    public GetOrderByIdHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _orderRepoMock = new Mock<IPrescriptionOrderRepository>();

        _unitOfWorkMock.Setup(u => u.PrescriptionOrders).Returns(_orderRepoMock.Object);

        _handler = new GetOrderByIdHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingOrder_ShouldReturnOrderDto()
    {
        // Arrange
        var order = new PrescriptionOrder
        {
            Id = TestOrderId,
            PatientId = PatientId,
            Patient = new Patient { Id = PatientId, FirstName = "John", LastName = "Doe", Email = "john@test.com" },
            PrescriptionId = PrescriptionId,
            Prescription = new Prescription
            {
                Id = PrescriptionId,
                MedicationName = "Aspirin",
                Dosage = "500mg",
                ExpiryDate = DateTime.UtcNow.AddDays(30)
            },
            Status = OrderStatus.Pending,
            OrderDate = DateTime.UtcNow
        };

        _orderRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(TestOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderByIdQuery(TestOrderId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(TestOrderId);
        result.Status.ShouldBe(OrderStatus.Pending);
        result.Prescription!.MedicationName.ShouldBe("Aspirin");
        result.Patient!.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        _orderRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(NonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrescriptionOrder?)null);

        var query = new GetOrderByIdQuery(NonExistentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }
}

