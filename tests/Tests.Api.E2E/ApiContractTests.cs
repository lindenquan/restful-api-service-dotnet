using System.Net;
using System.Text.Json;
using DTOs.Shared;
using DTOs.V2;
using Shouldly;
using Tests.Api.E2E.Fixtures;

namespace Tests.Api.E2E;

/// <summary>
/// API contract tests that verify clients can properly deserialize API responses.
/// These tests catch JSON serialization/deserialization issues like the OData format mismatch
/// that occurred when DemoApp tried to deserialize List&lt;T&gt; directly from OData responses.
/// </summary>
[Collection(nameof(ApiE2ETestCollection))]
public sealed class ApiContractTests
{
    private readonly ApiE2ETestFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiContractTests(ApiE2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that the Patients endpoint returns OData format with 'value' property.
    /// This is the format the DemoApp must parse correctly.
    /// </summary>
    [Fact]
    public async Task PatientsEndpoint_ReturnsODataFormat_WithValueProperty()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync("/api/v2/patients");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify OData structure
        root.TryGetProperty("value", out _).ShouldBeTrue(
            "Patients response should contain 'value' property (OData format)");
        root.TryGetProperty("@odata.count", out _).ShouldBeTrue(
            "Patients response should contain '@odata.count' property");
    }

    /// <summary>
    /// Verifies that patients can be deserialized from the OData 'value' property.
    /// This is how the DemoApp ApiClient must parse the response.
    /// </summary>
    [Fact]
    public async Task PatientsEndpoint_CanDeserializeFromValueProperty()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync("/api/v2/patients");
        var json = await response.Content.ReadAsStringAsync();

        // Parse like DemoApp does (extract from 'value' property)
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        List<PatientDto>? patients = null;
        if (root.TryGetProperty("value", out var valEl))
        {
            patients = JsonSerializer.Deserialize<List<PatientDto>>(valEl.GetRawText(), JsonOptions);
        }

        // Assert
        patients.ShouldNotBeNull("Should be able to deserialize patients from 'value' property");

        if (patients.Count > 0)
        {
            var first = patients[0];
            first.Id.ShouldNotBe(Guid.Empty, "Patient should have valid ID");
            first.FullName.ShouldNotBeNullOrEmpty("Patient should have FullName");
            first.Age.ShouldBeGreaterThanOrEqualTo(0, "Patient should have Age (V2 field)");
        }
    }

    /// <summary>
    /// Documents the bug: directly deserializing OData response to List fails.
    /// The OData response is a JSON object with 'value', not a JSON array.
    /// </summary>
    [Fact]
    public async Task PatientsEndpoint_DirectListDeserialization_Fails()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync("/api/v2/patients");
        var json = await response.Content.ReadAsStringAsync();

        // Assert - Direct deserialization to List<T> should throw
        // because the response is { "value": [...] } not [...]
        Should.Throw<JsonException>(() =>
        {
            JsonSerializer.Deserialize<List<PatientDto>>(json, JsonOptions);
        });
    }

    /// <summary>
    /// Verifies that Prescriptions endpoint also returns OData format.
    /// </summary>
    [Fact]
    public async Task PrescriptionsEndpoint_ReturnsODataFormat()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync("/api/v2/prescriptions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("value", out _).ShouldBeTrue(
            "Prescriptions response should contain 'value' property");
    }

    /// <summary>
    /// Verifies that Orders endpoint returns OData format.
    /// </summary>
    [Fact]
    public async Task OrdersEndpoint_ReturnsODataFormat()
    {
        // Act
        var response = await _fixture.AdminClient.GetAsync("/api/v2/orders");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("value", out _).ShouldBeTrue(
            "Orders response should contain 'value' property");
    }

    /// <summary>
    /// Verifies that protected endpoints require authentication.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_WithoutApiKey_Returns401()
    {
        // Act - Use unauthenticated client
        var response = await _fixture.Client.GetAsync("/api/v2/patients");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

