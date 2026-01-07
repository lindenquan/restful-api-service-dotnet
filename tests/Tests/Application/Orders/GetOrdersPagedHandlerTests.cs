using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using DTOs.Shared;
using Moq;
using Shouldly;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for GetOrdersPagedHandler with OData support.
/// </summary>
public class GetOrdersPagedHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPrescriptionOrderRepository> _orderRepoMock;
    private readonly GetOrdersPagedHandler _handler;

    public GetOrdersPagedHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _orderRepoMock = new Mock<IPrescriptionOrderRepository>();

        _unitOfWorkMock.Setup(u => u.PrescriptionOrders).Returns(_orderRepoMock.Object);

        _handler = new GetOrdersPagedHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidODataParameters_ShouldReturnPagedData()
    {
        // Arrange
        const int skip = 0;
        const int top = 20;
        const bool includeCount = true;
        const string orderBy = "orderDate";
        const bool descending = true;

        var orders = new List<PrescriptionOrder>
        {
            new PrescriptionOrder
            {
                Id = Guid.NewGuid(),
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                PatientId = Guid.NewGuid(),
                PrescriptionId = Guid.NewGuid()
            },
            new PrescriptionOrder
            {
                Id = Guid.NewGuid(),
                OrderDate = DateTime.UtcNow.AddDays(-1),
                Status = OrderStatus.Pending,
                PatientId = Guid.NewGuid(),
                PrescriptionId = Guid.NewGuid()
            }
        };

        var pagedData = new PagedData<PrescriptionOrder>(orders, 2);

        _orderRepoMock
            .Setup(r => r.GetPagedWithDetailsAsync(skip, top, includeCount, orderBy, descending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new GetOrdersPagedQuery(skip, top, includeCount, orderBy, descending);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Items[0].Status.ShouldBe(OrderStatus.Pending);
        result.Items[1].Status.ShouldBe(OrderStatus.Pending);

        _orderRepoMock.Verify(
            r => r.GetPagedWithDetailsAsync(skip, top, includeCount, orderBy, descending, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyResults_ShouldReturnEmptyPagedData()
    {
        // Arrange
        const int skip = 0;
        const int top = 20;
        var pagedData = new PagedData<PrescriptionOrder>(new List<PrescriptionOrder>(), 0);

        _orderRepoMock
            .Setup(r => r.GetPagedWithDetailsAsync(skip, top, false, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new GetOrdersPagedQuery(skip, top);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WithSortingDescending_ShouldPassParametersToRepository()
    {
        // Arrange
        const int skip = 40;
        const int top = 15;
        const bool includeCount = true;
        const string orderBy = "orderDate";
        const bool descending = true;

        var pagedData = new PagedData<PrescriptionOrder>(new List<PrescriptionOrder>(), 0);

        int? capturedSkip = null;
        int? capturedTop = null;
        bool? capturedIncludeCount = null;
        string? capturedOrderBy = null;
        bool? capturedDescending = null;

        _orderRepoMock
            .Setup(r => r.GetPagedWithDetailsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, int, bool, string?, bool, CancellationToken>((s, t, ic, ob, d, _) =>
            {
                capturedSkip = s;
                capturedTop = t;
                capturedIncludeCount = ic;
                capturedOrderBy = ob;
                capturedDescending = d;
            })
            .ReturnsAsync(pagedData);

        var query = new GetOrdersPagedQuery(skip, top, includeCount, orderBy, descending);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        capturedSkip.ShouldBe(skip);
        capturedTop.ShouldBe(top);
        capturedIncludeCount.ShouldBe(includeCount);
        capturedOrderBy.ShouldBe(orderBy);
        capturedDescending.ShouldBe(descending);
    }
}

