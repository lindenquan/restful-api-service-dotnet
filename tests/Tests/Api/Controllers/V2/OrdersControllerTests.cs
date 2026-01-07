using Application.Orders.Operations;
using Domain;
using DTOs.Shared;
using DTOs.V2;
using Infrastructure.Api.Controllers.V2;
using Infrastructure.Api.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shouldly;

namespace Tests.Api.Controllers.V2;

/// <summary>
/// Unit tests for V2 OrdersController with OData support.
/// </summary>
public class OrdersControllerTests
{
    private static readonly Guid OrderId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OrderId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PatientId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PrescriptionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid UserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly OrdersController _controller;
    private readonly PaginationSettings _paginationSettings;

    public OrdersControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _paginationSettings = new PaginationSettings { DefaultPageSize = 20, MaxPageSize = 100 };

        _currentUserMock.Setup(u => u.UserId).Returns(UserId);

        _controller = new OrdersController(_mediatorMock.Object, _currentUserMock.Object, _paginationSettings);

        // Set up HttpContext for pagination helper
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/api/v2/orders";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task GetAll_WithODataQuery_ShouldReturnOkWithPagedOrders()
    {
        // Arrange
        var orders = new List<PrescriptionOrder>
        {
            new() { Id = OrderId1, OrderDate = DateTime.UtcNow, Status = OrderStatus.Pending, PatientId = PatientId, PrescriptionId = PrescriptionId },
            new() { Id = OrderId2, OrderDate = DateTime.UtcNow.AddDays(-1), Status = OrderStatus.Completed, PatientId = PatientId, PrescriptionId = PrescriptionId }
        };
        var pagedData = new PagedData<PrescriptionOrder>(orders, 2);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetOrdersPagedQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new ODataQueryOptions { Top = 20, Skip = 0 };

        // Act
        var result = await _controller.GetAll(query, CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var pagedResult = okResult.Value.ShouldBeOfType<PagedResult<PrescriptionOrderDto>>();
        pagedResult.Value.ShouldNotBeNull();
        pagedResult.Value.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAll_WithSorting_ShouldPassParametersToHandler()
    {
        // Arrange
        var orders = new List<PrescriptionOrder>
        {
            new() { Id = OrderId1, OrderDate = DateTime.UtcNow, Status = OrderStatus.Pending, PatientId = PatientId, PrescriptionId = PrescriptionId }
        };
        var pagedData = new PagedData<PrescriptionOrder>(orders, 1);

        GetOrdersPagedQuery? capturedQuery = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetOrdersPagedQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PagedData<PrescriptionOrder>>, CancellationToken>((query, _) =>
            {
                if (query is GetOrdersPagedQuery pagedQuery)
                    capturedQuery = pagedQuery;
            })
            .ReturnsAsync(pagedData);

        var query = new ODataQueryOptions
        {
            Top = 10,
            Skip = 0,
            OrderBy = "orderDate desc"
        };

        // Act
        await _controller.GetAll(query, CancellationToken.None);

        // Assert
        capturedQuery.ShouldNotBeNull();
        capturedQuery!.Skip.ShouldBe(0);
        capturedQuery.Top.ShouldBe(10);
        capturedQuery.OrderBy.ShouldBe("orderDate");
        capturedQuery.Descending.ShouldBeTrue();
    }

    [Fact]
    public async Task GetByPatient_WithODataQuery_ShouldReturnOkWithPagedOrders()
    {
        // Arrange
        var orders = new List<PrescriptionOrder>
        {
            new() { Id = OrderId1, OrderDate = DateTime.UtcNow, Status = OrderStatus.Pending, PatientId = PatientId, PrescriptionId = PrescriptionId }
        };
        var pagedData = new PagedData<PrescriptionOrder>(orders, 1);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetOrdersByPatientPagedQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new ODataQueryOptions { Top = 20, Skip = 0 };

        // Act
        var result = await _controller.GetByPatient(PatientId, query, CancellationToken.None);

        // Assert
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var pagedResult = okResult.Value.ShouldBeOfType<PagedResult<PrescriptionOrderDto>>();
        pagedResult.Value.ShouldNotBeNull();
        pagedResult.Value.Count.ShouldBe(1);
    }
}

