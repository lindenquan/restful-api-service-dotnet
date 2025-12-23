using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Entities;
using FluentAssertions;
using Moq;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for GetOrderByIdHandler.
/// </summary>
public class GetOrderByIdHandlerTests
{
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
            Id = 1,
            PatientId = 1,
            Patient = new Patient { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@test.com" },
            PrescriptionId = 1,
            Prescription = new Prescription
            {
                Id = 1,
                MedicationName = "Aspirin",
                Dosage = "500mg",
                ExpiryDate = DateTime.UtcNow.AddDays(30)
            },
            Status = OrderStatus.Pending,
            OrderDate = DateTime.UtcNow
        };

        _orderRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderByIdQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Status.Should().Be(OrderStatus.Pending);  // ← Now comparing enum, not string
        result.MedicationName.Should().Be("Aspirin");
        result.PatientName.Should().Be("John Doe");
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        _orderRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrescriptionOrder?)null);

        var query = new GetOrderByIdQuery(999);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

