namespace Infrastructure.Cache;

/// <summary>
/// Cache configuration settings supporting Local (memory) and Remote (Redis) caching.
/// These are separate caches, not layered. Each endpoint chooses which cache to use via attributes.
/// </summary>
public class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Local in-memory cache settings (for static reference data).
    /// </summary>
    public LocalCacheSettings Local { get; set; } = new();

    /// <summary>
    /// Remote Redis cache settings (for dynamic data with consistency options).
    /// </summary>
    public RemoteCacheSettings Remote { get; set; } = new();
}

/// <summary>
/// Local in-memory cache settings.
/// <para>
/// <strong>Important:</strong> Local cache is for static reference data only with infinite TTL.
/// Cache entries are never automatically invalidated or expired. They are only evicted when:
/// <list type="bullet">
/// <item><description>The <see cref="MaxItems"/> limit is reached (LRU eviction)</description></item>
/// <item><description>The application restarts</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Use Local cache only for:</strong> Static reference data that never changes
/// (e.g., drug lists, ICD codes, configuration). Do NOT use for user data or transactional data.
/// </para>
/// </summary>
public class LocalCacheSettings
{
    /// <summary>
    /// Whether Local cache is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum number of items in Local cache.
    /// When this limit is reached, least recently used items are evicted.
    /// </summary>
    public int MaxItems { get; set; } = 10000;
}

/// <summary>
/// Remote Redis cache settings.
/// <para>
/// <strong>Startup behavior:</strong> When Remote cache is enabled, Redis must be healthy at startup.
/// The application will fail to start if Redis is unavailable.
/// </para>
/// <para>
/// <strong>Runtime behavior:</strong> If Redis becomes unavailable after startup, the application
/// continues to work without Remote cache. Errors are logged but requests are not blocked.
/// The health check will report "Degraded" status when Redis is down.
/// </para>
/// </summary>
public class RemoteCacheSettings
{
    /// <summary>
    /// Whether Remote cache is enabled.
    /// When enabled, Redis must be available at startup (required).
    /// After startup, Redis failures are gracefully handled (degraded mode).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Default consistency level for remote cache operations.
    /// Can be overridden per-endpoint using [RemoteCache(CacheConsistency.X)] attribute.
    /// <list type="bullet">
    /// <item><term>Eventual</term><description>TTL-based, stale OK for duration of TTL</description></item>
    /// <item><term>Strong</term><description>Lock-based, readers bypass to DB when locked (recommended)</description></item>
    /// <item><term>Serializable</term><description>Lock-based, readers wait for lock release</description></item>
    /// </list>
    /// </summary>
    public CacheConsistency Consistency { get; set; } = CacheConsistency.Strong;

    /// <summary>
    /// Redis connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Instance name prefix for Redis keys.
    /// </summary>
    public string InstanceName { get; set; } = "PrescriptionApi:";

    /// <summary>
    /// TTL for cache entries in seconds.
    /// Set to 0 or negative for infinite TTL (no expiration).
    /// </summary>
    public int TtlSeconds { get; set; } = 300;

    /// <summary>
    /// Gets the TTL as a TimeSpan, or null if TTL is infinite (TtlSeconds &lt;= 0).
    /// </summary>
    public TimeSpan? GetTtl() => TtlSeconds <= 0 ? null : TimeSpan.FromSeconds(TtlSeconds);

    /// <summary>
    /// Redis connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Redis operation timeout in milliseconds.
    /// </summary>
    public int SyncTimeout { get; set; } = 1000;

    /// <summary>
    /// Lock timeout in seconds. If a lock holder dies, the lock auto-expires after this duration.
    /// This is a safety net to prevent deadlocks. Used for Strong and Serializable consistency.
    /// </summary>
    public int LockTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Delay in milliseconds between lock retry attempts (for Serializable consistency).
    /// </summary>
    public int LockRetryDelayMs { get; set; } = 50;

    /// <summary>
    /// Maximum time in milliseconds to wait for a lock (for Serializable consistency).
    /// After this, the read will bypass the cache and go directly to DB.
    /// </summary>
    public int LockWaitTimeoutMs { get; set; } = 1000;

    /// <summary>Returns lock timeout as TimeSpan.</summary>
    public TimeSpan GetLockTimeout() => TimeSpan.FromSeconds(LockTimeoutSeconds);
}

/// <summary>
/// Cache consistency modes for Remote (Redis) cache.
/// Determines how cache reads behave when a write is in progress.
/// </summary>
public enum CacheConsistency
{
    /// <summary>
    /// TTL-based expiration only. No locking.
    /// <para><strong>Stale window:</strong> Up to configured TtlSeconds.</para>
    /// <para><strong>Best for:</strong> High-read, low-write data where brief staleness is acceptable.</para>
    /// </summary>
    Eventual,

    /// <summary>
    /// Lock-based consistency. Readers bypass cache and read from DB when locked.
    /// <para><strong>Stale window:</strong> 0 (no stale reads during writes).</para>
    /// <para><strong>Best for:</strong> Most write operations where consistency matters.</para>
    /// </summary>
    Strong,

    /// <summary>
    /// Lock-based consistency. Readers wait for lock release before reading.
    /// <para><strong>Stale window:</strong> 0 (strict ordering, no stale reads).</para>
    /// <para><strong>Best for:</strong> Critical operations requiring strict read/write ordering.</para>
    /// </summary>
    Serializable
}

