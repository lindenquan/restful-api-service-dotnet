using Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Infrastructure.Cache;

/// <summary>
/// Extension methods for registering Cache services.
/// <para>
/// <strong>Local Cache:</strong> In-memory cache for static reference data (infinite TTL).
/// </para>
/// <para>
/// <strong>Remote Cache (Redis):</strong>
/// <list type="bullet">
/// <item><description>Startup: Redis must be healthy. Application fails to start if Redis is unavailable.</description></item>
/// <item><description>Runtime: Redis failures are handled gracefully. Errors are logged and app continues without cache.</description></item>
/// </list>
/// </para>
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Add cache services with configurable Local (memory) and Remote (Redis) caches.
    /// These are separate caches - endpoints choose which to use via attributes.
    /// When Remote is enabled, Redis must be available at startup (fails fast).
    /// </summary>
    public static IServiceCollection AddCache(
        this IServiceCollection services,
        CacheSettings settings)
    {
        services.AddSingleton(settings);

        var localEnabled = settings.Local.Enabled;
        var remoteEnabled = settings.Remote.Enabled;

        // Register Local (Memory) cache if enabled
        if (localEnabled)
        {
            services.AddSingleton<IMemoryCache>(_ =>
            {
                var options = new MemoryCacheOptions
                {
                    SizeLimit = settings.Local.MaxItems
                };
                return new MemoryCache(options);
            });
            services.AddSingleton<LocalCacheService>();
            // Also register as ICacheService for CachingBehavior (MediatR pipeline)
            services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<LocalCacheService>());
        }
        else
        {
            // Register NullCacheService when local cache is disabled
            services.AddSingleton<ICacheService, NullCacheService>();
        }

        // Register Remote (Redis) cache if enabled
        if (remoteEnabled)
        {
            RegisterRedis(services, settings.Remote);
            services.AddSingleton<RemoteCacheService>();
        }

        // Register the CacheActionFilter with optional dependencies
        services.AddScoped(sp =>
        {
            var localCache = localEnabled ? sp.GetService<LocalCacheService>() : null;
            var remoteCache = remoteEnabled ? sp.GetService<RemoteCacheService>() : null;
            var logger = sp.GetRequiredService<ILogger<CacheActionFilter>>();
            return new CacheActionFilter(localCache, remoteCache, settings, logger);
        });

        return services;
    }

    private static void RegisterRedis(IServiceCollection services, RemoteCacheSettings settings)
    {
        var configurationOptions = ConfigurationOptions.Parse(settings.ConnectionString);
        configurationOptions.ConnectTimeout = settings.ConnectTimeout;
        configurationOptions.SyncTimeout = settings.SyncTimeout;
        // AbortOnConnectFail = true ensures startup fails if Redis is unavailable
        configurationOptions.AbortOnConnectFail = true;
        // Enable automatic reconnection after startup
        configurationOptions.ReconnectRetryPolicy = new ExponentialRetry(5000);

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RemoteCacheService>>();
            try
            {
                var connection = ConnectionMultiplexer.Connect(configurationOptions);
                logger.LogInformation("Successfully connected to Redis at {ConnectionString}", settings.ConnectionString);
                return connection;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Failed to connect to Redis at {ConnectionString}. Redis is required at startup when Remote cache is enabled.", settings.ConnectionString);
                throw;
            }
        });
    }
}
