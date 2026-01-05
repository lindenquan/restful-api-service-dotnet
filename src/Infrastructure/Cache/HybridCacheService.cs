using Application.Interfaces.Services;

namespace Infrastructure.Cache;

/// <summary>
/// Hybrid L1/L2 cache service that combines in-memory (L1) and Redis (L2) caching.
/// Supports configurable consistency modes for each layer.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class HybridCacheService : ICacheService, IDisposable
{
    private readonly MemoryCacheService? _l1Cache;
    private readonly RedisCacheService? _l2Cache;
    private readonly CacheSettings _settings;
    private readonly ILogger<HybridCacheService> _logger;
    private bool _disposed;

    public HybridCacheService(
        MemoryCacheService? l1Cache,
        RedisCacheService? l2Cache,
        CacheSettings settings,
        ILogger<HybridCacheService> logger)
    {
        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _settings = settings;
        _logger = logger;

        // Subscribe to L2 invalidation events for Strong consistency L1
        if (_l1Cache != null && _l2Cache != null && _settings.L1.Consistency == CacheConsistency.Strong)
        {
            _l2Cache.OnInvalidate += OnL2Invalidate;
            _logger.LogInformation("L1 cache configured with Strong consistency - listening for L2 invalidation events");
        }
    }

    private void OnL2Invalidate(string key)
    {
        _l1Cache?.Remove(key);
        _logger.LogDebug("L1 cache invalidated for key: {Key}", key);
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        // Write to L2 first (source of truth)
        _l2Cache?.Set(key, value, expiry);

        // Write to L1 with appropriate TTL (null means infinite)
        if (_l1Cache != null)
        {
            var l1Expiry = _settings.L1.Consistency == CacheConsistency.Eventual
                ? _settings.L1.GetTtl()
                : expiry;
            _l1Cache.Set(key, value, l1Expiry);
        }

        // Publish invalidation for Strong consistency (other instances)
        if (_settings.L1.Consistency == CacheConsistency.Strong)
        {
            _l2Cache?.PublishInvalidation(key);
        }
    }

    public T? Get<T>(string key)
    {
        TryGet<T>(key, out var value);
        return value;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        // Try L1 first
        if (_l1Cache != null && _l1Cache.TryGet<T>(key, out value))
        {
            return true;
        }

        // Try L2
        if (_l2Cache != null && _l2Cache.TryGet<T>(key, out value))
        {
            // Populate L1 from L2 (read-through) using L1 TTL (null means infinite)
            if (_l1Cache != null && value != null)
            {
                var l1Expiry = _settings.L1.GetTtl();
                _l1Cache.Set(key, value, l1Expiry);
            }
            return true;
        }

        value = default;
        return false;
    }

    public void Remove(string key)
    {
        // Remove from L1
        _l1Cache?.Remove(key);

        // Remove from L2 (this will publish invalidation for Strong consistency)
        _l2Cache?.Remove(key);
    }

    public bool Exists(string key)
    {
        // Check L1 first
        if (_l1Cache?.Exists(key) == true)
            return true;

        // Check L2
        return _l2Cache?.Exists(key) == true;
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

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_l2Cache != null)
        {
            _l2Cache.OnInvalidate -= OnL2Invalidate;
        }

        _l2Cache?.Dispose();
    }
}

