using Infrastructure.Persistence.Configuration;
using Infrastructure.Persistence.Security;
using Application.Interfaces.Repositories;
using Domain;
using Microsoft.Extensions.Options;

namespace Infrastructure.Api.Services;

/// <summary>
/// Background service that initializes the root admin user on startup.
/// Always ensures exactly one root-admin user exists with the configured API key.
/// Updates the root-admin user if it already exists.
/// </summary>
public class RootAdminInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RootAdminSettings _settings;
    private readonly ILogger<RootAdminInitializer> _logger;

    public RootAdminInitializer(
        IServiceProvider serviceProvider,
        IOptions<RootAdminSettings> settings,
        ILogger<RootAdminInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.EnableAutoCreate)
        {
            _logger.LogInformation("Root admin auto-creation is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.InitialApiKey))
        {
            _logger.LogWarning(
                "Root admin InitialApiKey is not configured. " +
                "Set RootAdmin:InitialApiKey in appsettings or environment variables.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        try
        {
            // Check if root-admin user already exists
            var existingRootAdmin = await unitOfWork.Users.GetByUserNameAsync(
                RootAdminSettings.UserName,
                cancellationToken);

            var apiKeyHash = ApiKeyHasher.HashApiKey(_settings.InitialApiKey);
            var apiKeyPrefix = ApiKeyHasher.GetKeyPrefix(_settings.InitialApiKey);

            if (existingRootAdmin != null)
            {
                // Update existing root-admin user with new credentials
                existingRootAdmin.ApiKeyHash = apiKeyHash;
                existingRootAdmin.ApiKeyPrefix = apiKeyPrefix;
                existingRootAdmin.Email = _settings.Email;
                existingRootAdmin.UserType = UserType.Admin;
                existingRootAdmin.IsActive = true;
                existingRootAdmin.Description = "System root admin - updated on startup";
                existingRootAdmin.Metadata.UpdatedBy = "SYSTEM";

                unitOfWork.Users.Update(existingRootAdmin);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Root admin user updated with username '{UserName}' and email '{Email}'. " +
                    "API key prefix: {KeyPrefix}. " +
                    "IMPORTANT: Change the InitialApiKey after first use or disable EnableAutoCreate!",
                    RootAdminSettings.UserName,
                    _settings.Email,
                    apiKeyPrefix);
            }
            else
            {
                // Create new root admin user
                var rootAdmin = new User
                {
                    ApiKeyHash = apiKeyHash,
                    ApiKeyPrefix = apiKeyPrefix,
                    UserName = RootAdminSettings.UserName,
                    Email = _settings.Email,
                    UserType = UserType.Admin,
                    IsActive = true,
                    Description = "System root admin - created on first startup",
                    Metadata = { CreatedBy = "SYSTEM" }
                };

                await unitOfWork.Users.AddAsync(rootAdmin, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Root admin user created with username '{UserName}' and email '{Email}'. " +
                    "API key prefix: {KeyPrefix}. " +
                    "IMPORTANT: Change the InitialApiKey after first use or disable EnableAutoCreate!",
                    RootAdminSettings.UserName,
                    _settings.Email,
                    apiKeyPrefix);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize root admin user");
            // Don't throw - let the application continue starting
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

