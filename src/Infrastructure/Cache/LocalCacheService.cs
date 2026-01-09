using System.Collections.Concurrent;
using Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Cache;

/// <summary>
/// Local in-memory cache service implementation using IMemoryCache.
/// <para>
/// This is a simple static data cache with infinite TTL. Cache entries are never
/// automatically invalidated or expired. They are only evicted when MaxItems limit
/// is reached (LRU eviction) or when the application restarts.
/// </para>
/// <para>
/// Use only for static reference data that never changes (e.g., drug lists, ICD codes).
/// </para>
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class LocalCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocalCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    public LocalCacheService(
        IMemoryCache cache,
        CacheSettings settings,
        ILogger<LocalCacheService> logger)
    {
        _cache = cache;
        _logger = logger;

        _logger.LogInformation(
            "Local cache initialized as static data cache (infinite TTL, MaxItems: {MaxItems}). " +
            "Use only for static reference data that never changes.",
            settings.Local.MaxItems);
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        // Local cache always uses infinite TTL - expiry parameter is ignored
        var options = new MemoryCacheEntryOptions
        {
            Size = 1 // Each entry counts as 1 item for size limiting
            // No AbsoluteExpiration or SlidingExpiration - infinite TTL
        };

        // Track the key and register callback to remove it when evicted
        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            _keys.TryRemove(evictedKey.ToString()!, out _);
        });

        _keys.TryAdd(key, 0);
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
        _keys.TryRemove(key, out _);
        _cache.Remove(key);
    }

    public void RemoveByPrefix(string prefix)
    {
        var keysToRemove = _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var key in keysToRemove)
        {
            Remove(key);
        }
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
