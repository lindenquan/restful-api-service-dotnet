using System.Net;
using System.Net.Http.Json;
using DTOs.Shared;
using DTOs.V2;
using Shouldly;
using Tests.Api.E2E.Fixtures;

namespace Tests.Api.E2E.V2;

/// <summary>
/// Request DTO for creating a V2 patient in E2E tests.
/// </summary>
public record CreatePatientV2TestRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime DateOfBirth,
    string? Notes = null
);

/// <summary>
/// E2E tests for V2 Patients API endpoints.
/// V2 includes additional fields like age calculation and search endpoint.
/// </summary>
[Collection(nameof(ApiE2ETestCollection))]
public sealed class PatientsApiV2E2ETests : IAsyncLifetime
{
    private const string BasePath = "/api/v2/patients";
    private readonly ApiE2ETestFixture _fixture;
    private readonly List<Guid> _createdPatientIds = new();

    public PatientsApiV2E2ETests(ApiE2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreatePatient_WithValidData_ShouldReturn201WithAge()
    {
        // Arrange
        var uniqueEmail = $"e2e-v2-{Guid.NewGuid():N}@example.com";
        var birthDate = DateTime.UtcNow.AddYears(-35).Date;
        var request = new CreatePatientV2TestRequest(
            FirstName: "V2Test",
            LastName: "Patient",
            Email: uniqueEmail,
            Phone: "555-0300",
            DateOfBirth: birthDate,
            Notes: "V2 test notes"
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var patient = await response.Content.ReadFromJsonAsync<PatientDto>();
        patient.ShouldNotBeNull();
        patient!.FirstName.ShouldBe("V2Test");
        patient.Age.ShouldBeGreaterThanOrEqualTo(34); // V2 includes age
        patient.CreatedAt.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        _createdPatientIds.Add(patient.Id);
    }

    [Fact]
    public async Task GetPatientById_ShouldReturnV2DtoWithAge()
    {
        // Arrange - create a patient first
        var uniqueEmail = $"e2e-v2-get-{Guid.NewGuid():N}@example.com";
        var createRequest = new CreatePatientV2TestRequest(
            FirstName: "GetV2",
            LastName: "Test",
            Email: uniqueEmail,
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-40).Date
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync(BasePath, createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();
        _createdPatientIds.Add(created!.Id);

        // Act
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/{created.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patient = await response.Content.ReadFromJsonAsync<PatientDto>();
        patient.ShouldNotBeNull();
        patient!.Id.ShouldBe(created.Id);
        patient.Age.ShouldBeGreaterThanOrEqualTo(39);
        patient.FullName.ShouldBe("GetV2 Test");
    }

    [Fact]
    public async Task GetAllPatients_ShouldReturnV2Dtos()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync(BasePath);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<PatientDto>>();
        result.ShouldNotBeNull();
        result!.Value.ShouldNotBeNull();
        result.Value.All(p => p.Age >= 0).ShouldBeTrue(); // V2 includes age
    }

    [Fact]
    public async Task SearchPatients_ByName_ShouldReturnFilteredResults()
    {
        // Arrange - create a patient with unique name
        var uniqueName = $"Search{Guid.NewGuid():N}";
        var createRequest = new CreatePatientV2TestRequest(
            FirstName: uniqueName,
            LastName: "Patient",
            Email: $"{uniqueName.ToLower()}@example.com",
            Phone: null,
            DateOfBirth: DateTime.UtcNow.AddYears(-30).Date
        );
        var createResponse = await _fixture.AdminClient.PostAsJsonAsync(BasePath, createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();
        _createdPatientIds.Add(created!.Id);

        // Act
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/search?name={uniqueName}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patients = await response.Content.ReadFromJsonAsync<IEnumerable<PatientDto>>();
        patients.ShouldNotBeNull();
        patients.ShouldContain(p => p.FirstName == uniqueName);
    }

    [Fact]
    public async Task SearchPatients_WithNoMatch_ShouldReturnEmptyList()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/search?name=NonExistentName12345");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patients = await response.Content.ReadFromJsonAsync<IEnumerable<PatientDto>>();
        patients.ShouldNotBeNull();
        patients.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreatePatient_WithoutApiKey_ShouldReturn401()
    {
        // Arrange
        var request = new CreatePatientV2TestRequest(
            FirstName: "NoAuth", LastName: "Patient", Email: "noauth@example.com",
            Phone: null, DateOfBirth: DateTime.UtcNow.AddYears(-30).Date
        );

        // Act
        var response = await _fixture.Client.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

