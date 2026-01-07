using Application.Interfaces.Repositories;
using Application.Orders.Operations;
using Domain;
using DTOs.Shared;
using Moq;
using Shouldly;

namespace Tests.Application.Orders;

/// <summary>
/// Unit tests for GetOrdersByPatientPagedHandler with OData support.
/// </summary>
public class GetOrdersByPatientPagedHandlerTests
{
    private static readonly Guid TestPatientId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPrescriptionOrderRepository> _orderRepoMock;
    private readonly GetOrdersByPatientPagedHandler _handler;

    public GetOrdersByPatientPagedHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _orderRepoMock = new Mock<IPrescriptionOrderRepository>();

        _unitOfWorkMock.Setup(u => u.PrescriptionOrders).Returns(_orderRepoMock.Object);

        _handler = new GetOrdersByPatientPagedHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidPatientIdAndODataParameters_ShouldReturnPagedData()
    {
        // Arrange
        const int skip = 0;
        const int top = 10;
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
                PatientId = TestPatientId,
                PrescriptionId = Guid.NewGuid()
            }
        };

        var pagedData = new PagedData<PrescriptionOrder>(orders, 1);

        _orderRepoMock
            .Setup(r => r.GetPagedByPatientWithDetailsAsync(TestPatientId, skip, top, includeCount, orderBy, descending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new GetOrdersByPatientPagedQuery(TestPatientId, skip, top, includeCount, orderBy, descending);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(1);
        result.Items[0].PatientId.ShouldBe(TestPatientId);

        _orderRepoMock.Verify(
            r => r.GetPagedByPatientWithDetailsAsync(TestPatientId, skip, top, includeCount, orderBy, descending, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoOrders_ShouldReturnEmptyPagedData()
    {
        // Arrange
        const int skip = 0;
        const int top = 20;
        var pagedData = new PagedData<PrescriptionOrder>(new List<PrescriptionOrder>(), 0);

        _orderRepoMock
            .Setup(r => r.GetPagedByPatientWithDetailsAsync(TestPatientId, skip, top, false, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new GetOrdersByPatientPagedQuery(TestPatientId, skip, top);

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
        const int skip = 10;
        const int top = 5;
        const bool includeCount = true;
        const string orderBy = "orderDate";
        const bool descending = true;

        var pagedData = new PagedData<PrescriptionOrder>(new List<PrescriptionOrder>(), 0);

        Guid? capturedPatientId = null;
        int? capturedSkip = null;
        int? capturedTop = null;
        bool? capturedIncludeCount = null;
        string? capturedOrderBy = null;
        bool? capturedDescending = null;

        _orderRepoMock
            .Setup(r => r.GetPagedByPatientWithDetailsAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, int, int, bool, string?, bool, CancellationToken>((pid, s, t, ic, ob, d, _) =>
            {
                capturedPatientId = pid;
                capturedSkip = s;
                capturedTop = t;
                capturedIncludeCount = ic;
                capturedOrderBy = ob;
                capturedDescending = d;
            })
            .ReturnsAsync(pagedData);

        var query = new GetOrdersByPatientPagedQuery(TestPatientId, skip, top, includeCount, orderBy, descending);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        capturedPatientId.ShouldBe(TestPatientId);
        capturedSkip.ShouldBe(skip);
        capturedTop.ShouldBe(top);
        capturedIncludeCount.ShouldBe(includeCount);
        capturedOrderBy.ShouldBe(orderBy);
        capturedDescending.ShouldBe(descending);
    }
}

