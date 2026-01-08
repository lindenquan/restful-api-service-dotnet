using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using Moq;
using Shouldly;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for UpdateOrderStatusHandler.
/// </summary>
public class UpdateOrderStatusHandlerTests
{
    private static readonly Guid TestOrderId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid NonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

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
            Id = TestOrderId,
            Status = OrderStatus.Pending
        };

        var updatedOrder = new PrescriptionOrder
        {
            Id = TestOrderId,
            Status = OrderStatus.Processing,
            Patient = new Patient { FirstName = "John", LastName = "Doe" },
            Prescription = new Prescription { MedicationName = "Aspirin" }
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdWithDetailsAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(updatedOrder);

        var command = new UpdateOrderStatusCommand(TestOrderId, OrderStatus.Processing, "Processing now");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        _orderRepoMock.Verify(r => r.UpdateAsync(It.Is<PrescriptionOrder>(o => o.Status == OrderStatus.Processing), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        _orderRepoMock.Setup(r => r.GetByIdAsync(NonExistentId, It.IsAny<CancellationToken>())).ReturnsAsync((PrescriptionOrder?)null);

        var command = new UpdateOrderStatusCommand(NonExistentId, OrderStatus.Processing, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
        _orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<PrescriptionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithReadyStatus_ShouldSetFulfilledDate()
    {
        // Arrange
        var order = new PrescriptionOrder { Id = TestOrderId, Status = OrderStatus.Processing };
        var updatedOrder = new PrescriptionOrder
        {
            Id = TestOrderId,
            Status = OrderStatus.Ready,
            Patient = new Patient { FirstName = "John", LastName = "Doe" },
            Prescription = new Prescription { MedicationName = "Aspirin" }
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdWithDetailsAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(updatedOrder);

        var command = new UpdateOrderStatusCommand(TestOrderId, OrderStatus.Ready, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _orderRepoMock.Verify(r => r.UpdateAsync(It.Is<PrescriptionOrder>(o => o.FulfilledDate != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCompletedStatus_ShouldSetPickupDate()
    {
        // Arrange
        var order = new PrescriptionOrder { Id = TestOrderId, Status = OrderStatus.Ready };
        var updatedOrder = new PrescriptionOrder
        {
            Id = TestOrderId,
            Status = OrderStatus.Completed,
            Patient = new Patient { FirstName = "John", LastName = "Doe" },
            Prescription = new Prescription { MedicationName = "Aspirin" }
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderRepoMock.Setup(r => r.GetByIdWithDetailsAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(updatedOrder);

        var command = new UpdateOrderStatusCommand(TestOrderId, OrderStatus.Completed, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _orderRepoMock.Verify(r => r.UpdateAsync(It.Is<PrescriptionOrder>(o => o.PickupDate != null), It.IsAny<CancellationToken>()), Times.Once);
    }
}

