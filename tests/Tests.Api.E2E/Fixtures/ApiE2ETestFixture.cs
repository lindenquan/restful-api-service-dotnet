using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Tests.Api.E2E.Fixtures;

/// <summary>
/// Test fixture for API E2E tests.
/// Uses WebApplicationFactory to spin up the API with E2E configuration.
/// Requires MongoDB and Redis to be running (via docker-compose).
/// </summary>
public sealed class ApiE2ETestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string AdminApiKey = "e2e-test-admin-api-key-12345";

    public HttpClient Client { get; private set; } = null!;
    public HttpClient AdminClient { get; private set; } = null!;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Force E2E environment
        builder.UseEnvironment("e2e");

        // Override configuration to use E2E settings
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
                .AddJsonFile("appsettings.e2e.json", optional: false)
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

        await Task.CompletedTask;
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

