using Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Adapters.Cache;

/// <summary>
/// Extension methods for registering Cache services.
/// <para>
/// <strong>L2 Cache (Redis) Availability:</strong>
/// <list type="bullet">
/// <item><description>Startup: Redis must be healthy. Application fails to start if Redis is unavailable.</description></item>
/// <item><description>Runtime: Redis failures are handled gracefully. Errors are logged and app continues without L2 cache.</description></item>
/// </list>
/// </para>
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Add cache services with configurable L1 (memory) and L2 (Redis) layers.
    /// When L2 is enabled, Redis must be available at startup (fails fast).
    /// After startup, Redis failures are logged as errors but don't block requests.
    /// </summary>
    public static IServiceCollection AddCache(
        this IServiceCollection services,
        CacheSettings settings)
    {
        services.AddSingleton(settings);

        // Check if any cache layer is enabled
        var l1Enabled = settings.L1.Enabled;
        var l2Enabled = settings.L2.Enabled;

        // If no cache is enabled, use NullCacheService
        if (!l1Enabled && !l2Enabled)
        {
            services.AddSingleton<ICacheService, NullCacheService>();
            return services;
        }

        // Register L1 (Memory) cache if enabled
        if (l1Enabled)
        {
            // Register IMemoryCache with size limit
            services.AddSingleton<IMemoryCache>(sp =>
            {
                var options = new MemoryCacheOptions
                {
                    SizeLimit = settings.L1.MaxItems
                };
                return new MemoryCache(options);
            });
            services.AddSingleton<MemoryCacheService>();
        }

        // Register L2 (Redis) cache if enabled
        if (l2Enabled)
        {
            RegisterRedis(services, settings.L2);
            services.AddSingleton<RedisCacheService>();
        }

        // Register the appropriate cache service based on configuration
        if (l1Enabled && l2Enabled)
        {
            // Both enabled: use HybridCacheService
            services.AddSingleton<ICacheService>(sp =>
            {
                var l1 = sp.GetRequiredService<MemoryCacheService>();
                var l2 = sp.GetRequiredService<RedisCacheService>();
                var logger = sp.GetRequiredService<ILogger<HybridCacheService>>();
                return new HybridCacheService(l1, l2, settings, logger);
            });
        }
        else if (l1Enabled)
        {
            // Only L1 enabled
            services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<MemoryCacheService>());
        }
        else
        {
            // Only L2 enabled
            services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<RedisCacheService>());
        }

        return services;
    }

    private static void RegisterRedis(IServiceCollection services, L2CacheSettings settings)
    {
        var configurationOptions = ConfigurationOptions.Parse(settings.ConnectionString);
        configurationOptions.ConnectTimeout = settings.ConnectTimeout;
        configurationOptions.SyncTimeout = settings.SyncTimeout;
        // AbortOnConnectFail = true (default) ensures startup fails if Redis is unavailable
        // After startup, the connection will auto-reconnect when Redis becomes available
        configurationOptions.AbortOnConnectFail = true;
        // Enable automatic reconnection after startup
        configurationOptions.ReconnectRetryPolicy = new ExponentialRetry(5000);

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
            try
            {
                var connection = ConnectionMultiplexer.Connect(configurationOptions);
                logger.LogInformation("Successfully connected to Redis at {ConnectionString}", settings.ConnectionString);
                return connection;
            }
            catch (Exception ex)
            {
                // Redis must be healthy at startup - fail fast
                logger.LogCritical(ex, "Failed to connect to Redis at {ConnectionString}. Redis is required at startup when L2 cache is enabled.", settings.ConnectionString);
                throw;
            }
        });
    }
}
