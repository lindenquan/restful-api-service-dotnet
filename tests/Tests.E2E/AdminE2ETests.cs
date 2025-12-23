using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Tests.E2E.Fixtures;

namespace Tests.E2E;

/// <summary>
/// E2E tests for Admin API endpoints.
/// Requires docker-compose.e2e.yml to be running.
/// Run: docker-compose -f docker-compose.e2e.yml up -d
/// </summary>
[Collection("E2E")]
public class AdminE2ETests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private const string AdminApiKey = "e2e-test-admin-api-key-12345";

    public AdminE2ETests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateApiKey_WithAdminKey_ShouldReturnCreated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-API-Key", AdminApiKey);
        var request = new
        {
            UserName = $"test-user-{Guid.NewGuid():N}",
            Email = $"test-{Guid.NewGuid():N}@example.com",
            UserType = 0, // Regular
            Description = "E2E test user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/api-keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        content.Should().NotBeNull();
        content!.ApiKey.Should().NotBeNullOrEmpty();
        content.UserName.Should().Be(request.UserName);
    }

    [Fact]
    public async Task CreateApiKey_WithoutApiKey_ShouldReturnUnauthorized()
    {
        // Arrange - no API key header
        var request = new
        {
            UserName = "test-user",
            Email = "test@example.com",
            UserType = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/api-keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateApiKey_WithRegularUserKey_ShouldReturnForbidden()
    {
        // First create a regular user
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-API-Key", AdminApiKey);

        var createUserRequest = new
        {
            UserName = $"regular-user-{Guid.NewGuid():N}",
            Email = $"regular-{Guid.NewGuid():N}@example.com",
            UserType = 0, // Regular
            Description = "Regular user for testing"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/admin/api-keys", createUserRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdUser = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var regularUserApiKey = createdUser!.ApiKey;

        // Now try to create another user with the regular user's key
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-API-Key", regularUserApiKey);

        var request = new
        {
            UserName = "another-user",
            Email = "another@example.com",
            UserType = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/api-keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateApiKey_WithDuplicateEmail_ShouldReturnConflict()
    {
        // Arrange
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-API-Key", AdminApiKey);

        var uniqueEmail = $"duplicate-{Guid.NewGuid():N}@example.com";
        var request = new
        {
            UserName = $"user-{Guid.NewGuid():N}",
            Email = uniqueEmail,
            UserType = 0
        };

        // Create first user
        var firstResponse = await _client.PostAsJsonAsync("/api/v1/admin/api-keys", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Try to create second user with same email
        var duplicateRequest = new
        {
            UserName = $"user-{Guid.NewGuid():N}",
            Email = uniqueEmail, // Same email
            UserType = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/api-keys", duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private record CreateApiKeyResponse(
        int UserId,
        string ApiKey,
        string ApiKeyPrefix,
        string UserName,
        string Email,
        string UserType,
        string Message);
}

