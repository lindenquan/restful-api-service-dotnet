using System.Net;
using System.Net.Http.Json;
using Domain;
using DTOs.Shared;
using Shouldly;
using Tests.Api.E2E.Fixtures;

namespace Tests.Api.E2E.V1;

/// <summary>
/// Request DTO for creating a prescription in E2E tests.
/// </summary>
public record CreatePrescriptionTestRequest(
    Guid PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions
);

/// <summary>
/// E2E tests for Prescriptions API endpoints.
/// Tests the API directly using HttpClient (no typed clients).
/// Requires MongoDB and Redis running via docker-compose.
/// All endpoints require API key authentication (uses AdminClient).
/// </summary>
[Collection(nameof(ApiE2ETestCollection))]
public sealed class PrescriptionsApiE2ETests : IAsyncLifetime
{
    private const string BasePath = "/api/v1/prescriptions";
    private readonly ApiE2ETestFixture _fixture;
    private readonly List<Guid> _createdPrescriptionIds = new();

    public PrescriptionsApiE2ETests(ApiE2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Note: No delete endpoint for prescriptions yet
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreatePrescription_WithValidData_ShouldReturn201()
    {
        // Arrange - use seeded patient
        var request = new CreatePrescriptionTestRequest(
            PatientId: _fixture.TestPatientId,
            MedicationName: "Ibuprofen",
            Dosage: "400mg",
            Frequency: "Twice daily",
            Quantity: 60,
            RefillsAllowed: 2,
            PrescriberName: "Dr. E2E Test",
            ExpiryDate: DateTime.UtcNow.AddMonths(12),
            Instructions: "Take with food"
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var prescription = await response.Content.ReadFromJsonAsync<Prescription>();
        prescription.ShouldNotBeNull();
        prescription!.MedicationName.ShouldBe("Ibuprofen");
        prescription.Dosage.ShouldBe("400mg");
        prescription.PatientId.ShouldBe(_fixture.TestPatientId);

        _createdPrescriptionIds.Add(prescription.Id);
    }

    [Fact]
    public async Task CreatePrescription_WithoutApiKey_ShouldReturn401()
    {
        // Arrange
        var request = new CreatePrescriptionTestRequest(
            PatientId: _fixture.TestPatientId,
            MedicationName: "Aspirin",
            Dosage: "500mg",
            Frequency: "Once daily",
            Quantity: 30,
            RefillsAllowed: 1,
            PrescriberName: "Dr. NoAuth",
            ExpiryDate: DateTime.UtcNow.AddMonths(6),
            Instructions: null
        );

        // Act - use Client (no API key)
        var response = await _fixture.Client.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePrescription_WithInvalidPatientId_ShouldReturn400()
    {
        // Arrange
        var request = new CreatePrescriptionTestRequest(
            PatientId: Guid.NewGuid(), // Non-existent patient
            MedicationName: "Test Med",
            Dosage: "100mg",
            Frequency: "Daily",
            Quantity: 30,
            RefillsAllowed: 0,
            PrescriberName: "Dr. Test",
            ExpiryDate: DateTime.UtcNow.AddMonths(6),
            Instructions: null
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPrescriptionById_ExistingPrescription_ShouldReturn200()
    {
        // Act - use seeded prescription
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/{_fixture.TestPrescriptionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var prescription = await response.Content.ReadFromJsonAsync<Prescription>();
        prescription.ShouldNotBeNull();
        prescription!.Id.ShouldBe(_fixture.TestPrescriptionId);
    }

    [Fact]
    public async Task GetPrescriptionById_NonExistentPrescription_ShouldReturn404()
    {
        // Act
        var nonExistentId = Guid.NewGuid();
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllPrescriptions_ShouldReturn200()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync(BasePath);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<Prescription>>();
        result.ShouldNotBeNull();
        result!.Value.ShouldNotBeNull();
    }
}

