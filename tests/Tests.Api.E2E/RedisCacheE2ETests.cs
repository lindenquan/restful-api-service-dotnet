using System.Net;
using System.Net.Http.Json;
using DTOs.V1;
using FluentAssertions;
using Tests.Api.E2E.Fixtures;

namespace Tests.Api.E2E;

/// <summary>
/// E2E tests for Redis cache integration.
/// Tests that the API correctly uses Redis for caching.
/// Requires MongoDB and Redis running via docker-compose.
/// </summary>
[Collection(nameof(ApiE2ETestCollection))]
public sealed class RedisCacheE2ETests : IAsyncLifetime
{
    private readonly ApiE2ETestFixture _fixture;
    private readonly List<string> _createdOrderIds = new();

    public RedisCacheE2ETests(ApiE2ETestFixture fixture)
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
    public async Task GetOrderById_CalledTwice_ShouldReturnSameData()
    {
        // Arrange - create a test order
        var createRequest = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "Redis cache test order"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        _createdOrderIds.Add(createdOrder!.Id.ToString());

        // Act - call twice (second call should hit cache)
        var response1 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        var order1 = await response1.Content.ReadFromJsonAsync<OrderDto>();

        var response2 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        var order2 = await response2.Content.ReadFromJsonAsync<OrderDto>();

        // Assert - both should return the same data
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        order1.Should().NotBeNull();
        order2.Should().NotBeNull();
        order1!.Id.Should().Be(order2!.Id);
        order1.Status.Should().Be(order2.Status);
        order1.Notes.Should().Be(order2.Notes);
    }

    [Fact]
    public async Task UpdateOrder_ShouldInvalidateCache()
    {
        // Arrange - create and cache an order
        var createRequest = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "Cache invalidation test"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        _createdOrderIds.Add(createdOrder!.Id.ToString());

        // Get the order (should be cached)
        var getResponse1 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        var order1 = await getResponse1.Content.ReadFromJsonAsync<OrderDto>();
        order1!.Status.Should().Be("Pending");

        // Act - update the order (should invalidate cache)
        var updateRequest = new UpdateOrderRequest(Status: "Processing");
        var updateResponse = await _fixture.AdminClient.PutAsJsonAsync($"/api/v1/orders/{createdOrder.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get the order again (should reflect the update, not cached value)
        var getResponse2 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        var order2 = await getResponse2.Content.ReadFromJsonAsync<OrderDto>();

        // Assert - should reflect the update
        order2.Should().NotBeNull();
        order2!.Status.Should().Be("Processing");
    }

    [Fact]
    public async Task DeleteOrder_ShouldInvalidateCache()
    {
        // Arrange - create and cache an order
        var createRequest = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "Delete cache test"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Get the order (should be cached)
        var getResponse1 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder!.Id}");
        getResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - delete the order (should invalidate cache)
        var deleteResponse = await _fixture.AdminClient.DeleteAsync($"/api/v1/orders/{createdOrder.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Get the order again (should return 404, not cached value)
        var getResponse2 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");

        // Assert - should return 404
        getResponse2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentGetRequests_ShouldAllSucceed()
    {
        // Arrange - create a test order
        var createRequest = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "Concurrent cache test"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        _createdOrderIds.Add(createdOrder!.Id.ToString());

        // Act - make 10 concurrent GET requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - all should succeed
        responses.Should().AllSatisfy(response =>
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        });
    }
}

