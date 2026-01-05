using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using FluentAssertions;
using Moq;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for UpdateOrderStatusHandler.
/// </summary>
public class UpdateOrderStatusHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPrescriptionOrderRepository> _orderRepoMock;
    private readonly UpdateOrderStatusHandler _handler;

    public UpdateOrderStatusHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _orderRepoMock = new Mock<IPrescriptionOrderRepository>();

        _unitOfWorkMock.Setup(u => u.PrescriptionOrders).Returns(_orderRepoMock.Object);

        _handler = new UpdateOrderStatusHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingOrder_ShouldUpdateStatus()
    {
        // Arrange
        var order = new PrescriptionOrder
        {
            Id = 1,
            Status = OrderStatus.Pending
        };

        var updatedOrder = new PrescriptionOrder
        {
            Id = 1,
            Status = OrderStatus.Processing,
            Patient = new Patient { FirstName = "John", LastName = "Doe" },
            Prescription = new Prescription { MedicationName = "Aspirin" }
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(updatedOrder);

        var command = new UpdateOrderStatusCommand(1, OrderStatus.Processing, "Processing now");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _orderRepoMock.Verify(r => r.Update(It.Is<PrescriptionOrder>(o => o.Status == OrderStatus.Processing)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        _orderRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((PrescriptionOrder?)null);

        var command = new UpdateOrderStatusCommand(999, OrderStatus.Processing, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _orderRepoMock.Verify(r => r.Update(It.IsAny<PrescriptionOrder>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithReadyStatus_ShouldSetFulfilledDate()
    {
        // Arrange
        var order = new PrescriptionOrder { Id = 1, Status = OrderStatus.Processing };
        var updatedOrder = new PrescriptionOrder
        {
            Id = 1,
            Status = OrderStatus.Ready,
            Patient = new Patient { FirstName = "John", LastName = "Doe" },
            Prescription = new Prescription { MedicationName = "Aspirin" }
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(updatedOrder);

        var command = new UpdateOrderStatusCommand(1, OrderStatus.Ready, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _orderRepoMock.Verify(r => r.Update(It.Is<PrescriptionOrder>(o => o.FulfilledDate != null)), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCompletedStatus_ShouldSetPickupDate()
    {
        // Arrange
        var order = new PrescriptionOrder { Id = 1, Status = OrderStatus.Ready };
        var updatedOrder = new PrescriptionOrder
        {
            Id = 1,
            Status = OrderStatus.Completed,
            Patient = new Patient { FirstName = "John", LastName = "Doe" },
            Prescription = new Prescription { MedicationName = "Aspirin" }
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(updatedOrder);

        var command = new UpdateOrderStatusCommand(1, OrderStatus.Completed, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _orderRepoMock.Verify(r => r.Update(It.Is<PrescriptionOrder>(o => o.PickupDate != null)), Times.Once);
    }
}

