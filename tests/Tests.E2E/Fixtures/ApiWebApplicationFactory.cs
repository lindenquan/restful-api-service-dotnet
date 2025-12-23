using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Tests.E2E.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for E2E tests.
/// Uses docker-compose.e2e.yml environment configuration.
/// </summary>
public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to e2e which will load appsettings.e2e.json
        builder.UseEnvironment("e2e");

        // Set the content root to the Adapters project directory
        var adaptersProjectPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "src", "Adapters"));

        builder.UseContentRoot(adaptersProjectPath);

        // Config folder is at solution root
        var configPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "config"));

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources
            config.Sources.Clear();

            // Set base path to config folder
            config.SetBasePath(configPath);

            // Add base appsettings.json first
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

            // Add e2e-specific configuration
            config.AddJsonFile("appsettings.e2e.json", optional: false, reloadOnChange: false);

            // Allow environment variables to override (for Docker)
            config.AddEnvironmentVariables();
        });
    }
}

