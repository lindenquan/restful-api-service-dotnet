using System.Net;
using System.Net.Http.Json;
using DTOs.V1;
using Shouldly;
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
    private readonly List<Guid> _createdOrderIds = new();

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
            PatientId: _fixture.TestPatientId,
            PrescriptionId: _fixture.TestPrescriptionId,
            Notes: "Redis cache test order"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        _createdOrderIds.Add(createdOrder!.Id);

        // Act - call twice (second call should hit cache)
        var response1 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        var order1 = await response1.Content.ReadFromJsonAsync<OrderDto>();

        var response2 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        var order2 = await response2.Content.ReadFromJsonAsync<OrderDto>();

        // Assert - both should return the same data
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        order1.ShouldNotBeNull();
        order2.ShouldNotBeNull();
        order1!.Id.ShouldBe(order2!.Id);
        order1.Status.ShouldBe(order2.Status);
        order1.Notes.ShouldBe(order2.Notes);
    }

    [Fact]
    public async Task UpdateOrder_ShouldInvalidateCache()
    {
        // Arrange - create and cache an order
        var createRequest = new CreateOrderRequest(
            PatientId: _fixture.TestPatientId,
            PrescriptionId: _fixture.TestPrescriptionId,
            Notes: "Cache invalidation test"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        _createdOrderIds.Add(createdOrder!.Id);

        // Get the order (should be cached)
        var getResponse1 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        var order1 = await getResponse1.Content.ReadFromJsonAsync<OrderDto>();
        order1!.Status.ShouldBe("Pending");

        // Act - update the order (should invalidate cache)
        var updateRequest = new UpdateOrderRequest(Status: "Processing");
        var updateResponse = await _fixture.AdminClient.PutAsJsonAsync($"/api/v1/orders/{createdOrder.Id}", updateRequest);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Get the order again (should reflect the update, not cached value)
        var getResponse2 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");
        var order2 = await getResponse2.Content.ReadFromJsonAsync<OrderDto>();

        // Assert - should reflect the update
        order2.ShouldNotBeNull();
        order2!.Status.ShouldBe("Processing");
    }

    [Fact]
    public async Task DeleteOrder_ShouldInvalidateCache()
    {
        // Arrange - create and cache an order
        var createRequest = new CreateOrderRequest(
            PatientId: _fixture.TestPatientId,
            PrescriptionId: _fixture.TestPrescriptionId,
            Notes: "Delete cache test"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Get the order (should be cached)
        var getResponse1 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder!.Id}");
        getResponse1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act - delete the order (should invalidate cache)
        var deleteResponse = await _fixture.AdminClient.DeleteAsync($"/api/v1/orders/{createdOrder.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Get the order again (should return 404, not cached value)
        var getResponse2 = await _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}");

        // Assert - should return 404
        getResponse2.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentGetRequests_ShouldAllSucceed()
    {
        // Arrange - create a test order
        var createRequest = new CreateOrderRequest(
            PatientId: _fixture.TestPatientId,
            PrescriptionId: _fixture.TestPrescriptionId,
            Notes: "Concurrent cache test"
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync("/api/v1/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        _createdOrderIds.Add(createdOrder!.Id);

        // Act - make 10 concurrent GET requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _fixture.AdminClient.GetAsync($"/api/v1/orders/{createdOrder.Id}"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - all should succeed
        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
    }
}

