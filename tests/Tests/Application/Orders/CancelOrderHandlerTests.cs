using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using FluentAssertions;
using Moq;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for CancelOrderHandler.
/// </summary>
public class CancelOrderHandlerTests
{
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
            Id = 1,
            Status = OrderStatus.Pending
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var command = new CancelOrderCommand(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        _orderRepoMock.Verify(r => r.Update(order), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnFalse()
    {
        // Arrange
        _orderRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((PrescriptionOrder?)null);

        var command = new CancelOrderCommand(999);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _orderRepoMock.Verify(r => r.Update(It.IsAny<PrescriptionOrder>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCompletedOrder_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var order = new PrescriptionOrder
        {
            Id = 1,
            Status = OrderStatus.Completed
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var command = new CancelOrderCommand(1);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel a completed order*");
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
            Id = 1,
            Status = status
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var command = new CancelOrderCommand(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }
}

