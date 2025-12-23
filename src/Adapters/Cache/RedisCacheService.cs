using System.Text.Json;
using Application.Interfaces.Services;
using StackExchange.Redis;

namespace Adapters.Cache;

/// <summary>
/// L2 Redis-based distributed cache service implementation.
/// Supports pub/sub for cache invalidation in Strong consistency mode.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class RedisCacheService : ICacheService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly CacheSettings _cacheSettings;
    private readonly L2CacheSettings _settings;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Event raised when a cache invalidation message is received.
    /// Used by HybridCacheService to invalidate L1 cache.
    /// </summary>
    public event Action<string>? OnInvalidate;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        CacheSettings cacheSettings,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _cacheSettings = cacheSettings;
        _settings = cacheSettings.L2;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Subscribe to invalidation channel for Strong consistency
        if (_settings.Consistency == CacheConsistency.Strong)
        {
            SubscribeToInvalidation();
        }
    }

    private void SubscribeToInvalidation()
    {
        try
        {
            _subscriber.Subscribe(
                RedisChannel.Literal(_settings.InvalidationChannel),
                (_, message) =>
                {
                    if (message.HasValue)
                    {
                        var key = message.ToString();
                        _logger.LogDebug("Received cache invalidation for key: {Key}", key);
                        OnInvalidate?.Invoke(key);
                    }
                });
            _logger.LogInformation("Subscribed to cache invalidation channel: {Channel}", _settings.InvalidationChannel);
        }
        catch (Exception ex)
        {
            // Log error but continue - app works without L2 cache after startup
            _logger.LogError(ex, "Failed to subscribe to cache invalidation channel. Continuing without L2 cache pub/sub.");
        }
    }

    /// <summary>
    /// Publish cache invalidation message to all instances.
    /// </summary>
    public void PublishInvalidation(string key)
    {
        if (_settings.Consistency != CacheConsistency.Strong)
            return;

        try
        {
            _subscriber.Publish(RedisChannel.Literal(_settings.InvalidationChannel), key);
            _logger.LogDebug("Published cache invalidation for key: {Key}", key);
        }
        catch (Exception ex)
        {
            // Log error but continue - app works without L2 cache after startup
            _logger.LogError(ex, "Failed to publish cache invalidation for key {Key}. Continuing without L2 cache pub/sub.", key);
        }
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var fullKey = GetFullKey(key);
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var actualExpiry = expiry ?? TimeSpan.FromSeconds(_cacheSettings.L2.TtlSeconds);
            _database.StringSet(fullKey, json, actualExpiry);
        }
        catch (Exception ex)
        {
            // Log error but continue - app works without L2 cache after startup
            _logger.LogError(ex, "Failed to set L2 cache key {Key}. Continuing without L2 cache.", key);
        }
    }

    public T? Get<T>(string key)
    {
        TryGet<T>(key, out var value);
        return value;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        try
        {
            var fullKey = GetFullKey(key);
            var json = _database.StringGet(fullKey);

            if (json.IsNullOrEmpty)
            {
                value = default;
                return false;
            }

            value = JsonSerializer.Deserialize<T>((string)json!, _jsonOptions);
            return value != null;
        }
        catch (Exception ex)
        {
            // Log error but continue - app works without L2 cache after startup
            _logger.LogError(ex, "Failed to get L2 cache key {Key}. Continuing without L2 cache.", key);
            value = default;
            return false;
        }
    }

    public void Remove(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            _database.KeyDelete(fullKey);
            PublishInvalidation(key);
        }
        catch (Exception ex)
        {
            // Log error but continue - app works without L2 cache after startup
            _logger.LogError(ex, "Failed to remove L2 cache key {Key}. Continuing without L2 cache.", key);
        }
    }

    public bool Exists(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            return _database.KeyExists(fullKey);
        }
        catch (Exception ex)
        {
            // Log error but continue - app works without L2 cache after startup
            _logger.LogError(ex, "Failed to check L2 cache key {Key}. Continuing without L2 cache.", key);
            return false;
        }
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

    private string GetFullKey(string key) => $"{_settings.InstanceName}{key}";

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _subscriber.UnsubscribeAll();
        }
        catch
        {
            // Ignore disposal errors
        }
    }
}

