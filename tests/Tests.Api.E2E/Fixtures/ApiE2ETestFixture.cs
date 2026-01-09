using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Tests.Api.E2E.Fixtures;

/// <summary>
/// Test fixture for API E2E tests.
///
/// Supports two modes:
///   1. Local mode: Tests against API running in Docker (localhost:8080)
///   2. Remote mode: Tests against deployed API (dev/stage/prod)
///
/// Environment variables:
///   - ASPNETCORE_ENVIRONMENT: Target environment (local, dev, stage, prod)
///   - API_BASE_URL: Override base URL (optional, derived from environment if not set)
///   - API_KEY: Admin API key for authentication
///
/// Usage:
///   Local:  ASPNETCORE_ENVIRONMENT=local (uses Docker Compose API at localhost:8080)
///   Remote: ASPNETCORE_ENVIRONMENT=dev API_BASE_URL=https://api.dev.example.com
/// </summary>
public sealed class ApiE2ETestFixture : IAsyncLifetime, IDisposable
{
    /// <summary>
    /// Test patient ID - populated during seeding.
    /// </summary>
    public Guid TestPatientId { get; private set; }

    /// <summary>
    /// Test prescription ID - populated during seeding.
    /// </summary>
    public Guid TestPrescriptionId { get; private set; }

    private readonly string _environment;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public HttpClient Client { get; private set; } = null!;
    public HttpClient AdminClient { get; private set; } = null!;

    public ApiE2ETestFixture()
    {
        _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "local";
        _apiKey = GetApiKey();
        _baseUrl = GetBaseUrl();
        _httpClient = new HttpClient();
    }

    public async Task InitializeAsync()
    {
        // Create HTTP clients pointing to the real API
        Client = new HttpClient { BaseAddress = new Uri(_baseUrl) };

        AdminClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        AdminClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);

        // Wait for API to be healthy
        await WaitForApiHealthAsync();

        // Seed test data via API (not direct DB access)
        await SeedTestDataViaApiAsync();
    }

    /// <summary>
    /// Waits for the API to be healthy before running tests.
    /// </summary>
    private async Task WaitForApiHealthAsync()
    {
        const int MaxRetries = 30;
        const int DelayMs = 1000;

        for (var i = 0; i < MaxRetries; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // API not ready yet
            }

            await Task.Delay(DelayMs);
        }

        throw new TimeoutException($"API at {_baseUrl} did not become healthy within {MaxRetries} seconds");
    }

    /// <summary>
    /// Seeds test data via API calls (works for both local and remote).
    /// Stores created IDs in TestPatientId and TestPrescriptionId properties.
    /// Always creates fresh test data to ensure prescription belongs to patient and has refills.
    /// </summary>
    private async Task SeedTestDataViaApiAsync()
    {
        // Always create a fresh test patient to ensure clean state
        var patientRequest = new
        {
            FirstName = "E2EOrderTest",
            LastName = "Patient",
            Email = $"e2e-order-{Guid.NewGuid():N}@test.com",
            Phone = "555-0100",
            DateOfBirth = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var createPatientResponse = await AdminClient.PostAsJsonAsync("/api/v1/patients", patientRequest);
        if (createPatientResponse.IsSuccessStatusCode)
        {
            var createdPatient = await createPatientResponse.Content.ReadFromJsonAsync<PatientDetailDto>();
            TestPatientId = createdPatient!.Id;
        }
        else
        {
            throw new InvalidOperationException($"Failed to create test patient: {await createPatientResponse.Content.ReadAsStringAsync()}");
        }

        // Always create a fresh prescription for the test patient with refills
        var prescriptionRequest = new
        {
            PatientId = TestPatientId,
            MedicationName = "E2E-OrderTest-Amoxicillin",
            Dosage = "500mg",
            Frequency = "Three times daily",
            Quantity = 30,
            RefillsAllowed = 12, // Maximum allowed refills per validation rules
            PrescriberName = "Dr. E2E OrderTest",
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            Instructions = "Take with food"
        };
        var createPrescriptionResponse = await AdminClient.PostAsJsonAsync("/api/v1/prescriptions", prescriptionRequest);
        if (createPrescriptionResponse.IsSuccessStatusCode)
        {
            var createdPrescription = await createPrescriptionResponse.Content.ReadFromJsonAsync<PrescriptionDetailDto>();
            TestPrescriptionId = createdPrescription!.Id;
        }
        else
        {
            throw new InvalidOperationException($"Failed to create test prescription: {await createPrescriptionResponse.Content.ReadAsStringAsync()}");
        }
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Client?.Dispose();
        AdminClient?.Dispose();
        _httpClient.Dispose();
    }

    /// <summary>
    /// Gets the API base URL based on environment.
    /// </summary>
    private string GetBaseUrl()
    {
        // Check for explicit override
        var explicitUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
        if (!string.IsNullOrEmpty(explicitUrl))
        {
            return explicitUrl.TrimEnd('/');
        }

        // Default URLs per environment
        return _environment.ToLowerInvariant() switch
        {
            "local" => "http://localhost:8080",
            "dev" => Environment.GetEnvironmentVariable("API_BASE_URL_DEV")
                     ?? throw new InvalidOperationException("API_BASE_URL or API_BASE_URL_DEV must be set for dev environment"),
            "stage" => Environment.GetEnvironmentVariable("API_BASE_URL_STAGE")
                       ?? throw new InvalidOperationException("API_BASE_URL or API_BASE_URL_STAGE must be set for stage environment"),
            "prod" => throw new InvalidOperationException("E2E tests should not run against production"),
            _ => throw new InvalidOperationException($"Unknown environment: {_environment}")
        };
    }

    /// <summary>
    /// Gets the API key for authentication.
    /// </summary>
    private string GetApiKey()
    {
        // Check for explicit API key
        var apiKey = Environment.GetEnvironmentVariable("API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            return apiKey;
        }

        // For local environment, use the default development API key
        if (_environment.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            // Load from config file
            var configPath = FindConfigDirectory(Directory.GetCurrentDirectory());
            if (configPath != null)
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(configPath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.local.json", optional: true)
                    .Build();

                var configKey = config["RootAdmin:InitialApiKey"];
                if (!string.IsNullOrEmpty(configKey))
                {
                    return configKey;
                }
            }

            // Fallback to well-known development key
            return "root-api-key-change-in-production-12345";
        }

        throw new InvalidOperationException($"API_KEY environment variable must be set for {_environment} environment");
    }

    /// <summary>
    /// Searches for the config directory by walking up the directory tree.
    /// </summary>
    private static string? FindConfigDirectory(string startPath)
    {
        var currentDir = new DirectoryInfo(startPath);

        while (currentDir != null)
        {
            var configPath = Path.Combine(currentDir.FullName, "config");
            if (Directory.Exists(configPath))
            {
                return configPath;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    // Helper DTOs for seeding - minimal records with just the Id we need
    private sealed record PagedApiResponse<T>(IReadOnlyList<T> Value);
    private sealed record PatientDetailDto(Guid Id);
    private sealed record PrescriptionDetailDto(Guid Id);
}

