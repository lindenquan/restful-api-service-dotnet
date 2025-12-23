using Adapters.ApiClient.V1;
using DTOs.V1;
using FluentAssertions;
using NSubstitute;
using Web.Services;

namespace Tests.Web.Services;

/// <summary>
/// Unit tests for OrderService business logic.
/// Tests business logic without UI components.
/// </summary>
public class OrderServiceTests
{
    private readonly IOrdersApiClient _mockOrdersApi;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _mockOrdersApi = Substitute.For<IOrdersApiClient>();
        _sut = new OrderService(_mockOrdersApi);
    }

    [Fact]
    public async Task GetAllOrdersAsync_ShouldReturnOrders()
    {
        // Arrange
        var expectedOrders = new List<OrderDto>
        {
            new(1, 1, "John Doe", 1, "Aspirin", DateTime.UtcNow, "Pending", null),
            new(2, 2, "Jane Smith", 2, "Ibuprofen", DateTime.UtcNow, "Processing", null)
        };
        _mockOrdersApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(expectedOrders);

        // Act
        var result = await _sut.GetAllOrdersAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedOrders);
        await _mockOrdersApi.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrdersByPatientAsync_ShouldReturnPatientOrders()
    {
        // Arrange
        var patientId = 1;
        var expectedOrders = new List<OrderDto>
        {
            new(1, patientId, "John Doe", 1, "Aspirin", DateTime.UtcNow, "Pending", null)
        };
        _mockOrdersApi.GetByPatientAsync(patientId, Arg.Any<CancellationToken>()).Returns(expectedOrders);

        // Act
        var result = await _sut.GetOrdersByPatientAsync(patientId);

        // Assert
        result.Should().BeEquivalentTo(expectedOrders);
        await _mockOrdersApi.Received(1).GetByPatientAsync(patientId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrderByIdAsync_ShouldReturnOrder()
    {
        // Arrange
        var orderId = 1;
        var expectedOrder = new OrderDto(orderId, 1, "John Doe", 1, "Aspirin", DateTime.UtcNow, "Pending", null);
        _mockOrdersApi.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(expectedOrder);

        // Act
        var result = await _sut.GetOrderByIdAsync(orderId);

        // Assert
        result.Should().BeEquivalentTo(expectedOrder);
        await _mockOrdersApi.Received(1).GetByIdAsync(orderId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldCreateOrder()
    {
        // Arrange
        var request = new CreateOrderRequest(1, 1, "Test notes");
        var expectedOrder = new OrderDto(1, 1, "John Doe", 1, "Aspirin", DateTime.UtcNow, "Pending", "Test notes");
        _mockOrdersApi.CreateAsync(request, Arg.Any<CancellationToken>()).Returns(expectedOrder);

        // Act
        var result = await _sut.CreateOrderAsync(request);

        // Assert
        result.Should().BeEquivalentTo(expectedOrder);
        await _mockOrdersApi.Received(1).CreateAsync(request, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 1, "Patient ID must be greater than 0")]
    [InlineData(-1, 1, "Patient ID must be greater than 0")]
    [InlineData(1, 0, "Prescription ID must be greater than 0")]
    [InlineData(1, -1, "Prescription ID must be greater than 0")]
    public void ValidateCreateOrderRequest_WithInvalidData_ShouldReturnError(int patientId, int prescriptionId, string expectedError)
    {
        // Arrange
        var request = new CreateOrderRequest(patientId, prescriptionId, null);

        // Act
        var result = _sut.ValidateCreateOrderRequest(request);

        // Assert
        result.Should().Be(expectedError);
    }

    [Fact]
    public void ValidateCreateOrderRequest_WithValidData_ShouldReturnNull()
    {
        // Arrange
        var request = new CreateOrderRequest(1, 1, "Test notes");

        // Act
        var result = _sut.ValidateCreateOrderRequest(request);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("pending", "warning")]
    [InlineData("Pending", "warning")]
    [InlineData("processing", "info")]
    [InlineData("ready", "primary")]
    [InlineData("completed", "success")]
    [InlineData("cancelled", "danger")]
    [InlineData("unknown", "secondary")]
    public void GetStatusBadgeColor_ShouldReturnCorrectColor(string status, string expectedColor)
    {
        // Act
        var result = _sut.GetStatusBadgeColor(status);

        // Assert
        result.Should().Be(expectedColor);
    }

    [Theory]
    [InlineData("pending", true)]
    [InlineData("Pending", true)]
    [InlineData("processing", true)]
    [InlineData("Processing", true)]
    [InlineData("ready", false)]
    [InlineData("completed", false)]
    [InlineData("cancelled", false)]
    public void CanCancelOrder_ShouldReturnCorrectResult(string status, bool expected)
    {
        // Act
        var result = _sut.CanCancelOrder(status);

        // Assert
        result.Should().Be(expected);
    }
}

