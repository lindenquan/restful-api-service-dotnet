using Infrastructure.Persistence.Configuration;
using Application.Interfaces.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Api.Services;

/// <summary>
/// Health check that verifies a root admin user exists.
/// Returns Unhealthy if no root admin user is found in the database.
/// </summary>
public class RootAdminHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RootAdminHealthCheck> _logger;

    public RootAdminHealthCheck(
        IServiceProvider serviceProvider,
        ILogger<RootAdminHealthCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var rootAdmin = await unitOfWork.Users.GetByUserNameAsync(
                RootAdminSettings.UserName,
                cancellationToken);

            if (rootAdmin == null)
            {
                _logger.LogError(
                    "Root admin user '{UserName}' not found. " +
                    "Ensure RootAdmin:InitialApiKey is configured and RootAdmin:EnableAutoCreate is true.",
                    RootAdminSettings.UserName);

                return HealthCheckResult.Unhealthy(
                    $"Root admin user '{RootAdminSettings.UserName}' not found. " +
                    "Configure RootAdmin:InitialApiKey environment variable.");
            }

            if (!rootAdmin.IsActive)
            {
                _logger.LogWarning("Root admin user exists but is not active");
                return HealthCheckResult.Degraded("Root admin user exists but is not active");
            }

            return HealthCheckResult.Healthy($"Root admin user '{RootAdminSettings.UserName}' exists and is active");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check root admin health");
            return HealthCheckResult.Unhealthy("Failed to verify root admin user", ex);
        }
    }
}

