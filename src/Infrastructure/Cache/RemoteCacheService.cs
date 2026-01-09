using System.Text.Json;
using StackExchange.Redis;

namespace Infrastructure.Cache;

/// <summary>
/// Result of a cache read operation with lock-awareness.
/// </summary>
public readonly struct CacheReadResult<T>
{
    public bool Found { get; init; }
    public T? Value { get; init; }
    public bool IsLocked { get; init; }

    public static CacheReadResult<T> Hit(T value) => new() { Found = true, Value = value, IsLocked = false };
    public static CacheReadResult<T> Miss() => new() { Found = false, Value = default, IsLocked = false };
    public static CacheReadResult<T> Locked() => new() { Found = false, Value = default, IsLocked = true };
}

/// <summary>
/// Remote Redis-based cache service with lock-based consistency.
/// <para>
/// Supports three consistency levels:
/// <list type="bullet">
/// <item><term>Eventual</term><description>TTL-based, no locking</description></item>
/// <item><term>Strong</term><description>Lock on write, readers bypass to DB when locked</description></item>
/// <item><term>Serializable</term><description>Lock on write, readers wait for lock release</description></item>
/// </list>
/// </para>
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class RemoteCacheService : IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly RemoteCacheSettings _settings;
    private readonly ILogger<RemoteCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    private const string LockPrefix = "lock:";

    public RemoteCacheService(
        IConnectionMultiplexer redis,
        CacheSettings cacheSettings,
        ILogger<RemoteCacheService> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _settings = cacheSettings.Remote;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        _logger.LogInformation(
            "Remote cache initialized (TtlSeconds: {Ttl}, LockTimeout: {LockTimeout}s)",
            _settings.TtlSeconds,
            _settings.LockTimeoutSeconds);
    }

    #region Lock Operations

    /// <summary>
    /// Acquire a lock for the specified key. Used during write operations.
    /// </summary>
    /// <param name="key">The cache key to lock.</param>
    /// <returns>A lock token if acquired, null otherwise.</returns>
    public string? AcquireLock(string key)
    {
        var lockKey = GetLockKey(key);
        var lockToken = Guid.NewGuid().ToString();
        var lockTimeout = _settings.GetLockTimeout();

        try
        {
            var acquired = _database.StringSet(lockKey, lockToken, lockTimeout, When.NotExists);
            if (acquired)
            {
                _logger.LogDebug("Lock acquired for key: {Key}", key);
                return lockToken;
            }

            _logger.LogDebug("Failed to acquire lock for key: {Key} (already locked)", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire lock for key {Key}.", key);
            return null;
        }
    }

    /// <summary>
    /// Release a lock for the specified key.
    /// </summary>
    /// <param name="key">The cache key to unlock.</param>
    /// <param name="lockToken">The token received when the lock was acquired.</param>
    public void ReleaseLock(string key, string lockToken)
    {
        var lockKey = GetLockKey(key);

        try
        {
            // Only release if we own the lock (compare token)
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            _database.ScriptEvaluate(script, [lockKey], [lockToken]);
            _logger.LogDebug("Lock released for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release lock for key {Key}.", key);
        }
    }

    /// <summary>
    /// Check if a key is currently locked.
    /// </summary>
    public bool IsLocked(string key)
    {
        var lockKey = GetLockKey(key);
        try
        {
            return _database.KeyExists(lockKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check lock status for key {Key}.", key);
            return false;
        }
    }

    /// <summary>
    /// Wait for a lock to be released (for Serializable consistency).
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if lock was released within timeout, false otherwise.</returns>
    public async Task<bool> WaitForLockReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(key);
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(_settings.LockWaitTimeoutMs);
        var retryDelay = TimeSpan.FromMilliseconds(_settings.LockRetryDelayMs);

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_database.KeyExists(lockKey))
            {
                _logger.LogDebug("Lock released for key: {Key}", key);
                return true;
            }

            await Task.Delay(retryDelay, cancellationToken);
        }

        _logger.LogDebug("Timeout waiting for lock release on key: {Key}", key);
        return false;
    }

    private string GetLockKey(string key) => $"{_settings.InstanceName}{LockPrefix}{key}";

    #endregion

    #region Cache Operations

    /// <summary>
    /// Set a value in the cache.
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var fullKey = GetFullKey(key);
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var actualExpiry = expiry ?? _settings.GetTtl();

            if (actualExpiry.HasValue)
            {
                _database.StringSet(fullKey, json, actualExpiry.Value);
            }
            else
            {
                _database.StringSet(fullKey, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cache key {Key}.", key);
        }
    }

    /// <summary>
    /// Get a value from the cache without lock awareness.
    /// </summary>
    public T? Get<T>(string key)
    {
        TryGet<T>(key, out var value);
        return value;
    }

    /// <summary>
    /// Try to get a value from the cache.
    /// </summary>
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
            _logger.LogError(ex, "Failed to get cache key {Key}.", key);
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Get a value from the cache with lock awareness.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="consistency">The consistency level for this read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cache read result indicating hit, miss, or locked status.</returns>
    public async Task<CacheReadResult<T>> GetWithConsistencyAsync<T>(
        string key,
        CacheConsistency consistency,
        CancellationToken cancellationToken = default)
    {
        // For Eventual consistency, just read the cache
        if (consistency == CacheConsistency.Eventual)
        {
            return TryGet<T>(key, out var value) ? CacheReadResult<T>.Hit(value!) : CacheReadResult<T>.Miss();
        }

        // For Strong/Serializable, check lock first
        if (IsLocked(key))
        {
            if (consistency == CacheConsistency.Strong)
            {
                // Strong: bypass cache, signal caller to read from DB
                _logger.LogDebug("Key {Key} is locked, bypassing cache (Strong consistency)", key);
                return CacheReadResult<T>.Locked();
            }

            // Serializable: wait for lock release
            _logger.LogDebug("Key {Key} is locked, waiting for release (Serializable consistency)", key);
            var released = await WaitForLockReleaseAsync(key, cancellationToken);
            if (!released)
            {
                // Timeout - bypass to DB as fallback
                _logger.LogWarning("Timeout waiting for lock on key {Key}, bypassing cache", key);
                return CacheReadResult<T>.Locked();
            }
        }

        // Lock released or not locked - read cache
        return TryGet<T>(key, out var cachedValue) ? CacheReadResult<T>.Hit(cachedValue!) : CacheReadResult<T>.Miss();
    }

    /// <summary>
    /// Remove a value from the cache.
    /// </summary>
    public void Remove(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            _database.KeyDelete(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache key {Key}.", key);
        }
    }

    /// <summary>
    /// Remove all cache entries matching the given prefix.
    /// </summary>
    public void RemoveByPrefix(string prefix)
    {
        try
        {
            var fullPrefix = GetFullKey(prefix);
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{fullPrefix}*").ToArray();

            if (keys.Length > 0)
            {
                _database.KeyDelete(keys);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache keys with prefix {Prefix}.", prefix);
        }
    }

    /// <summary>
    /// Check if a key exists in the cache.
    /// </summary>
    public bool Exists(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            return _database.KeyExists(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check cache key {Key}.", key);
            return false;
        }
    }

    private string GetFullKey(string key) => $"{_settings.InstanceName}{key}";

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        // Connection is managed externally, don't dispose it here
    }
}

