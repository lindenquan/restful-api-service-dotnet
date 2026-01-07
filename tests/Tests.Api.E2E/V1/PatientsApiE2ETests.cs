using System.Net;
using System.Net.Http.Json;
using Domain;
using DTOs.Shared;
using Shouldly;
using Tests.Api.E2E.Fixtures;

namespace Tests.Api.E2E.V1;

/// <summary>
/// Request DTO for creating a patient in E2E tests.
/// </summary>
public record CreatePatientTestRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime DateOfBirth
);

/// <summary>
/// E2E tests for Patients API endpoints.
/// Tests the API directly using HttpClient (no typed clients).
/// Requires MongoDB and Redis running via docker-compose.
/// All endpoints require API key authentication (uses AdminClient).
/// </summary>
[Collection(nameof(ApiE2ETestCollection))]
public sealed class PatientsApiE2ETests : IAsyncLifetime
{
    private const string BasePath = "/api/v1/patients";
    private readonly ApiE2ETestFixture _fixture;
    private readonly List<Guid> _createdPatientIds = new();

    public PatientsApiE2ETests(ApiE2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Note: No delete endpoint for patients yet, so cleanup is limited
        // In a real scenario, we might use direct database access for cleanup
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreatePatient_WithValidData_ShouldReturn201()
    {
        // Arrange
        var uniqueEmail = $"e2e-test-{Guid.NewGuid():N}@example.com";
        var request = new CreatePatientTestRequest(
            FirstName: "E2E",
            LastName: "TestPatient",
            Email: uniqueEmail,
            Phone: "555-0199",
            DateOfBirth: new DateTime(1985, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var patient = await response.Content.ReadFromJsonAsync<Patient>();
        patient.ShouldNotBeNull();
        patient!.FirstName.ShouldBe("E2E");
        patient.LastName.ShouldBe("TestPatient");
        patient.Email.ShouldBe(uniqueEmail);
        patient.Phone.ShouldBe("555-0199");
        patient.FullName.ShouldBe("E2E TestPatient");

        _createdPatientIds.Add(patient.Id);
    }

    [Fact]
    public async Task CreatePatient_WithNullPhone_ShouldReturn201()
    {
        // Arrange
        var uniqueEmail = $"e2e-test-{Guid.NewGuid():N}@example.com";
        var request = new CreatePatientTestRequest(
            FirstName: "NoPhone",
            LastName: "Patient",
            Email: uniqueEmail,
            Phone: null,
            DateOfBirth: new DateTime(1990, 3, 20, 0, 0, 0, DateTimeKind.Utc)
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var patient = await response.Content.ReadFromJsonAsync<Patient>();
        patient.ShouldNotBeNull();
        patient!.Phone.ShouldBeNull();

        _createdPatientIds.Add(patient.Id);
    }

    [Fact]
    public async Task CreatePatient_WithInvalidEmail_ShouldReturn400()
    {
        // Arrange
        var request = new CreatePatientTestRequest(
            FirstName: "Invalid",
            LastName: "Email",
            Email: "not-an-email",
            Phone: null,
            DateOfBirth: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePatient_WithEmptyFirstName_ShouldReturn400()
    {
        // Arrange
        var request = new CreatePatientTestRequest(
            FirstName: "",
            LastName: "NoFirstName",
            Email: "valid@example.com",
            Phone: null,
            DateOfBirth: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePatient_WithoutApiKey_ShouldReturn401()
    {
        // Arrange
        var request = new CreatePatientTestRequest(
            FirstName: "NoAuth",
            LastName: "Patient",
            Email: "noauth@example.com",
            Phone: null,
            DateOfBirth: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        // Act - use Client (no API key) instead of AdminClient
        var response = await _fixture.Client.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPatientById_ExistingPatient_ShouldReturn200()
    {
        // Arrange - create a patient first
        var uniqueEmail = $"e2e-get-{Guid.NewGuid():N}@example.com";
        var createRequest = new CreatePatientTestRequest(
            FirstName: "GetTest",
            LastName: "Patient",
            Email: uniqueEmail,
            Phone: "555-0200",
            DateOfBirth: new DateTime(1975, 12, 25, 0, 0, 0, DateTimeKind.Utc)
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync(BasePath, createRequest);
        var createdPatient = await createResponse.Content.ReadFromJsonAsync<Patient>();
        _createdPatientIds.Add(createdPatient!.Id);

        // Act
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/{createdPatient.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patient = await response.Content.ReadFromJsonAsync<Patient>();
        patient.ShouldNotBeNull();
        patient!.Id.ShouldBe(createdPatient.Id);
        patient.FirstName.ShouldBe("GetTest");
    }

    [Fact]
    public async Task GetPatientById_NonExistentPatient_ShouldReturn404()
    {
        // Act
        var nonExistentId = Guid.NewGuid();
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllPatients_ShouldReturn200()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync(BasePath);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<Patient>>();
        result.ShouldNotBeNull();
        result!.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreatePatient_WithDuplicateEmail_ShouldReturn400()
    {
        // Arrange - create a patient first
        var uniqueEmail = $"e2e-dup-{Guid.NewGuid():N}@example.com";
        var firstRequest = new CreatePatientTestRequest(
            FirstName: "First",
            LastName: "Patient",
            Email: uniqueEmail,
            Phone: null,
            DateOfBirth: new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        var firstResponse = await _fixture.AdminClient.PostAsJsonAsync(BasePath, firstRequest);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var firstPatient = await firstResponse.Content.ReadFromJsonAsync<Patient>();
        _createdPatientIds.Add(firstPatient!.Id);

        // Act - try to create another patient with same email
        var duplicateRequest = new CreatePatientTestRequest(
            FirstName: "Duplicate",
            LastName: "Email",
            Email: uniqueEmail,
            Phone: null,
            DateOfBirth: new DateTime(1985, 5, 5, 0, 0, 0, DateTimeKind.Utc)
        );
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, duplicateRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

