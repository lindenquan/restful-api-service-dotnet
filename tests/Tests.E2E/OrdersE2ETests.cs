using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Tests.E2E.Fixtures;

namespace Tests.E2E;

/// <summary>
/// E2E tests for Orders API endpoints.
/// Requires docker-compose.e2e.yml to be running.
/// Run: docker-compose -f docker-compose.e2e.yml up -d
/// </summary>
[Collection("E2E")]
public class OrdersE2ETests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;
    private const string AdminApiKey = "e2e-test-admin-api-key-12345";

    public OrdersE2ETests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", AdminApiKey);
    }

    [Fact]
    public async Task GetOrders_WithValidApiKey_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOrders_WithoutApiKey_ShouldReturnUnauthorized()
    {
        // Arrange - create a new client from the factory without API key
        var clientWithoutKey = _factory.CreateClient();
        // Don't add X-API-Key header

        // Act
        var response = await clientWithoutKey.GetAsync("/api/v1/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrderById_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/orders/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidOrder = new
        {
            UserId = 0, // Invalid - must be positive
            PrescriptionId = 0 // Invalid - must be positive
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/orders", invalidOrder);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

