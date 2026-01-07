using System.Net;
using System.Net.Http.Json;
using DTOs.V2;
using Shouldly;
using Tests.Api.E2E.Fixtures;

namespace Tests.Api.E2E.V2;

/// <summary>
/// Request DTO for creating a V2 prescription in E2E tests.
/// </summary>
public record CreatePrescriptionV2TestRequest(
    Guid PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions,
    bool IsControlledSubstance = false
);

/// <summary>
/// E2E tests for V2 Prescriptions API endpoints.
/// V2 includes additional endpoints and fields.
/// </summary>
[Collection(nameof(ApiE2ETestCollection))]
public sealed class PrescriptionsApiV2E2ETests : IAsyncLifetime
{
    private const string BasePath = "/api/v2/prescriptions";
    private readonly ApiE2ETestFixture _fixture;
    private readonly List<Guid> _createdPrescriptionIds = new();

    public PrescriptionsApiV2E2ETests(ApiE2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreatePrescription_WithValidData_ShouldReturn201WithDaysUntilExpiry()
    {
        // Arrange
        var expiryDate = DateTime.UtcNow.AddMonths(12);
        var request = new CreatePrescriptionV2TestRequest(
            PatientId: _fixture.TestPatientId,
            MedicationName: "V2 Test Med",
            Dosage: "100mg",
            Frequency: "Once daily",
            Quantity: 30,
            RefillsAllowed: 2,
            PrescriberName: "Dr. V2 Test",
            ExpiryDate: expiryDate,
            Instructions: "Take as directed",
            IsControlledSubstance: false
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var prescription = await response.Content.ReadFromJsonAsync<PrescriptionDto>();
        prescription.ShouldNotBeNull();
        prescription!.MedicationName.ShouldBe("V2 Test Med");
        prescription.DaysUntilExpiry.ShouldBeGreaterThan(300); // V2 includes days until expiry

        _createdPrescriptionIds.Add(prescription.Id);
    }

    [Fact]
    public async Task CreatePrescription_WithControlledSubstance_ShouldAddPrefix()
    {
        // Arrange
        var request = new CreatePrescriptionV2TestRequest(
            PatientId: _fixture.TestPatientId,
            MedicationName: "Controlled Med",
            Dosage: "5mg",
            Frequency: "As needed",
            Quantity: 30,
            RefillsAllowed: 0,
            PrescriberName: "Dr. V2 Test",
            ExpiryDate: DateTime.UtcNow.AddMonths(1),
            Instructions: "Pain management",
            IsControlledSubstance: true
        );

        // Act
        var response = await _fixture.AdminClient.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var prescription = await response.Content.ReadFromJsonAsync<PrescriptionDto>();
        prescription.ShouldNotBeNull();
        prescription!.Instructions.ShouldStartWith("[CONTROLLED SUBSTANCE]");

        _createdPrescriptionIds.Add(prescription.Id);
    }

    [Fact]
    public async Task GetPrescriptionById_ShouldReturnV2DtoWithDaysUntilExpiry()
    {
        // Act - use seeded prescription
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/{_fixture.TestPrescriptionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var prescription = await response.Content.ReadFromJsonAsync<PrescriptionDto>();
        prescription.ShouldNotBeNull();
        prescription!.Id.ShouldBe(_fixture.TestPrescriptionId);
        prescription.DaysUntilExpiry.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetByPatient_ShouldReturnPatientPrescriptions()
    {
        // Act - get prescriptions for seeded patient
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/patient/{_fixture.TestPatientId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var prescriptions = await response.Content.ReadFromJsonAsync<IEnumerable<PrescriptionDto>>();
        prescriptions.ShouldNotBeNull();
        prescriptions.ShouldAllBe(p => p.PatientId == _fixture.TestPatientId);
    }

    [Fact]
    public async Task GetActive_ShouldReturnOnlyActiveNonExpired()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/active");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var prescriptions = await response.Content.ReadFromJsonAsync<IEnumerable<PrescriptionDto>>();
        prescriptions.ShouldNotBeNull();
        prescriptions.ShouldAllBe(p => !p.IsExpired && p.CanRefill);
    }

    [Fact]
    public async Task GetExpired_ShouldReturnOnlyExpired()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync($"{BasePath}/expired");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var prescriptions = await response.Content.ReadFromJsonAsync<IEnumerable<PrescriptionDto>>();
        prescriptions.ShouldNotBeNull();
        // All returned should be expired (or empty if none expired)
        prescriptions.ShouldAllBe(p => p.IsExpired);
    }

    [Fact]
    public async Task CreatePrescription_WithoutApiKey_ShouldReturn401()
    {
        // Arrange
        var request = new CreatePrescriptionV2TestRequest(
            PatientId: _fixture.TestPatientId, MedicationName: "NoAuth", Dosage: "100mg", Frequency: "Daily",
            Quantity: 30, RefillsAllowed: 0, PrescriberName: "Dr. Test",
            ExpiryDate: DateTime.UtcNow.AddMonths(6), Instructions: null
        );

        // Act
        var response = await _fixture.Client.PostAsJsonAsync(BasePath, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

