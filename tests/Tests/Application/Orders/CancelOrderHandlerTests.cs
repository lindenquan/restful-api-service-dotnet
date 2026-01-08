using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using Moq;
using Shouldly;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for CancelOrderHandler.
/// </summary>
public class CancelOrderHandlerTests
{
    private static readonly Guid TestOrderId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid NonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPrescriptionOrderRepository> _orderRepoMock;
    private readonly CancelOrderHandler _handler;

    public CancelOrderHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _orderRepoMock = new Mock<IPrescriptionOrderRepository>();

        _unitOfWorkMock.Setup(u => u.PrescriptionOrders).Returns(_orderRepoMock.Object);

        _handler = new CancelOrderHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingPendingOrder_ShouldCancelAndReturnTrue()
    {
        // Arrange
        var order = new PrescriptionOrder
        {
            Id = TestOrderId,
            Status = OrderStatus.Pending
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var command = new CancelOrderCommand(TestOrderId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Cancelled);
        _orderRepoMock.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnFalse()
    {
        // Arrange
        _orderRepoMock.Setup(r => r.GetByIdAsync(NonExistentId, It.IsAny<CancellationToken>())).ReturnsAsync((PrescriptionOrder?)null);

        var command = new CancelOrderCommand(NonExistentId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
        _orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<PrescriptionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCompletedOrder_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var order = new PrescriptionOrder
        {
            Id = TestOrderId,
            Status = OrderStatus.Completed
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var command = new CancelOrderCommand(TestOrderId);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.ShouldContain("Cannot cancel a completed order");
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Ready)]
    public async Task Handle_WithNonCompletedOrder_ShouldSucceed(OrderStatus status)
    {
        // Arrange
        var order = new PrescriptionOrder
        {
            Id = TestOrderId,
            Status = status
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(TestOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var command = new CancelOrderCommand(TestOrderId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Cancelled);
    }
}

