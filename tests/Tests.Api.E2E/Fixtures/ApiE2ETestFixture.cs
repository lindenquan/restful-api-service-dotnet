using Application.Interfaces.Repositories;
using Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tests.Api.E2E.Fixtures;

/// <summary>
/// Test fixture for API E2E tests.
/// Uses WebApplicationFactory to spin up the API with environment-specific configuration.
///
/// Environment:
///   - local: Uses Docker Compose (localhost:27017, localhost:6379)
///   - dev/stage/prod: Uses real connection strings from config files
///
/// Set via ASPNETCORE_ENVIRONMENT environment variable (defaults to "local").
/// </summary>
public sealed class ApiE2ETestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string AdminApiKey = "root-api-key-change-in-production-12345";

    public HttpClient Client { get; private set; } = null!;
    public HttpClient AdminClient { get; private set; } = null!;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Use environment from ASPNETCORE_ENVIRONMENT (defaults to "local")
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "local";
        builder.UseEnvironment(environment);

        // Override configuration to use environment-specific settings
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Find the config directory - search upwards from current directory
            var currentDir = Directory.GetCurrentDirectory();
            var configPath = FindConfigDirectory(currentDir);

            if (configPath == null)
            {
                throw new DirectoryNotFoundException(
                    $"Could not find 'config' directory. Searched from: {currentDir}");
            }

            config.Sources.Clear();
            config
                .SetBasePath(configPath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables();
        });

        return base.CreateHost(builder);
    }

    public async Task InitializeAsync()
    {
        // Create HTTP clients
        Client = CreateClient();

        // Create admin client with API key
        AdminClient = CreateClient();
        AdminClient.DefaultRequestHeaders.Add("X-API-Key", AdminApiKey);

        // Seed test data (Patient ID 1 and Prescription ID 1)
        await SeedTestDataAsync();
    }

    /// <summary>
    /// Seeds test data required for E2E tests.
    /// Creates a test patient (ID: 1) and prescription (ID: 1) if they don't exist.
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        using var scope = Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Check if patient already exists
        var existingPatient = await unitOfWork.Patients.GetByIdAsync(1);
        if (existingPatient == null)
        {
            var patient = new Patient
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@test.com",
                Phone = "555-0100",
                DateOfBirth = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            await unitOfWork.Patients.AddAsync(patient);
            await unitOfWork.SaveChangesAsync();
        }

        // Check if prescription already exists
        var existingPrescription = await unitOfWork.Prescriptions.GetByIdAsync(1);
        if (existingPrescription == null)
        {
            var prescription = new Prescription
            {
                Id = 1,
                PatientId = 1,
                MedicationName = "Amoxicillin",
                Dosage = "500mg",
                Frequency = "Three times daily",
                Quantity = 30,
                RefillsRemaining = 2,
                PrescriberName = "Dr. Smith",
                PrescribedDate = DateTime.UtcNow.AddDays(-7),
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                Instructions = "Take with food"
            };
            await unitOfWork.Prescriptions.AddAsync(prescription);
            await unitOfWork.SaveChangesAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        Client?.Dispose();
        AdminClient?.Dispose();
        await base.DisposeAsync();
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
}

