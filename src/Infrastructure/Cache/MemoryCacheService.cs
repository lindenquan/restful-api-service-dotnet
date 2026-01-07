using Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Cache;

/// <summary>
/// L1 in-memory cache service implementation using IMemoryCache.
/// Provides thread-safe caching with automatic eviction and size limits.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheSettings _settings;
    private readonly ILogger<MemoryCacheService> _logger;

    public MemoryCacheService(
        IMemoryCache cache,
        CacheSettings settings,
        ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        // Use provided expiry, or get from settings (null means infinite TTL)
        var actualExpiry = expiry ?? _settings.L1.GetTtl();

        var options = new MemoryCacheEntryOptions
        {
            Size = 1 // Each entry counts as 1 item for size limiting
        };

        // Only set expiration if TTL is not infinite
        if (actualExpiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = actualExpiry.Value;
        }

        _cache.Set(key, value, options);
    }

    public T? Get<T>(string key)
    {
        TryGet<T>(key, out var value);
        return value;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        return _cache.TryGetValue(key, out value);
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }

    public bool Exists(string key)
    {
        return _cache.TryGetValue(key, out _);
    }

    public T GetOrAdd<T>(string key, Func<T> factory, TimeSpan? expiry = null)
    {
        if (TryGet<T>(key, out var cached) && cached != null)
            return cached;

        var value = factory();
        Set(key, value, expiry);
        return value;
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        if (TryGet<T>(key, out var cached) && cached != null)
            return cached;

        var value = await factory();
        Set(key, value, expiry);
        return value;
    }
}
