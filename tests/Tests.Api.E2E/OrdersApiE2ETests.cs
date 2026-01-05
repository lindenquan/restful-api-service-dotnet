using System.Net;
using System.Net.Http.Json;
using DTOs.V1;
using FluentAssertions;
using Tests.Api.E2E.Fixtures;

namespace Tests.Api.E2E;

/// <summary>
/// E2E tests for Orders API endpoints.
/// Tests the API directly using HttpClient (no typed clients).
/// Requires MongoDB and Redis running via docker-compose.
/// </summary>
[Collection(nameof(ApiE2ETestCollection))]
public sealed class OrdersApiE2ETests : IAsyncLifetime
{
    private readonly ApiE2ETestFixture _fixture;
    private readonly List<string> _createdOrderIds = new();

    public OrdersApiE2ETests(ApiE2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Cleanup: delete all created orders
        foreach (var orderId in _createdOrderIds)
        {
            try
            {
                await _fixture.AdminClient.DeleteAsync($"/api/v1/orders/{orderId}");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task CreateOrder_WithValidData_ShouldReturn201()
    {
        // Arrange
        var request = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "E2E test order"
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.Should().NotBeNull();
        order!.PatientId.Should().Be(1);
        order.PrescriptionId.Should().Be(1);
        order.Status.Should().Be("Pending");

        _createdOrderIds.Add(order.Id.ToString());
    }

    [Fact]
    public async Task GetOrderById_ExistingOrder_ShouldReturn200()
    {
        // Arrange - create an order first
        var createRequest = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "Test order for GET"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        _createdOrderIds.Add(createdOrder!.Id.ToString());

        // Act
        var response = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.Should().NotBeNull();
        order!.Id.Should().Be(createdOrder.Id);
    }

    [Fact]
    public async Task GetOrderById_NonExistentOrder_ShouldReturn404()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync("/api/v1/orders/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrder_WithValidData_ShouldReturn200()
    {
        // Arrange - create an order first
        var createRequest = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "Order to update"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        _createdOrderIds.Add(createdOrder!.Id.ToString());

        // Act
        var updateRequest = new UpdateOrderRequest(Status: "Processing");
        var response = await _fixture.AdminClient.PutAsJsonAsync($"/api/v1/orders/{createdOrder.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be("Processing");
    }

    [Fact]
    public async Task DeleteOrder_ExistingOrder_ShouldReturn204()
    {
        // Arrange - create an order first
        var createRequest = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "Order to delete"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Act
        var response = await _fixture.AdminClient.DeleteAsync($"/api/v1/orders/{createdOrder!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllOrders_ShouldReturn200()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync("/api/v1/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<IEnumerable<OrderDto>>();
        orders.Should().NotBeNull();
    }
}

