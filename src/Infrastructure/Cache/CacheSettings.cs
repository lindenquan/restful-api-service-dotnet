namespace Infrastructure.Cache;

/// <summary>
/// Cache configuration settings supporting L1 (memory) and L2 (Redis) caching.
/// </summary>
public class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>
    /// L1 in-memory cache settings.
    /// </summary>
    public L1CacheSettings L1 { get; set; } = new();

    /// <summary>
    /// L2 Redis distributed cache settings.
    /// </summary>
    public L2CacheSettings L2 { get; set; } = new();
}

/// <summary>
/// L1 in-memory cache settings.
/// </summary>
public class L1CacheSettings
{
    /// <summary>
    /// Whether L1 cache is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Consistency mode: Strong (invalidation via pub/sub) or Eventual (short TTL).
    /// Default is Strong for healthcare data integrity.
    /// </summary>
    public CacheConsistency Consistency { get; set; } = CacheConsistency.Strong;

    /// <summary>
    /// TTL for L1 cache entries in seconds.
    /// For Eventual consistency, keep this short (5-30 seconds).
    /// Set to 0 or negative for infinite TTL (no expiration).
    /// </summary>
    public int TtlSeconds { get; set; } = 30;

    /// <summary>
    /// Gets the TTL as a TimeSpan, or null if TTL is infinite (TtlSeconds &lt;= 0).
    /// </summary>
    public TimeSpan? GetTtl() => TtlSeconds <= 0 ? null : TimeSpan.FromSeconds(TtlSeconds);

    /// <summary>
    /// Maximum number of items in L1 cache.
    /// </summary>
    public int MaxItems { get; set; } = 10000;
}

/// <summary>
/// L2 Redis distributed cache settings.
/// <para>
/// <strong>Startup behavior:</strong> When L2 cache is enabled, Redis must be healthy at startup.
/// The application will fail to start if Redis is unavailable.
/// </para>
/// <para>
/// <strong>Runtime behavior:</strong> If Redis becomes unavailable after startup, the application
/// continues to work without L2 cache. Errors are logged but requests are not blocked.
/// The health check will report "Degraded" status when Redis is down.
/// </para>
/// </summary>
public class L2CacheSettings
{
    /// <summary>
    /// Whether L2 cache is enabled.
    /// When enabled, Redis must be available at startup (required).
    /// After startup, Redis failures are gracefully handled (degraded mode).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Consistency mode: Strong (write-through) or Eventual (write-behind).
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
    /// TTL for L2 cache entries in seconds.
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
    /// Channel name for cache invalidation pub/sub.
    /// </summary>
    public string InvalidationChannel { get; set; } = "cache:invalidate";
}

/// <summary>
/// Cache consistency modes.
/// <para>
/// <strong>Important:</strong> Neither mode provides perfect consistency.
/// For zero tolerance of stale data, disable caching entirely.
/// </para>
/// </summary>
public enum CacheConsistency
{
    /// <summary>
    /// Strong consistency: Near real-time invalidation via Redis pub/sub.
    /// <para>
    /// <strong>Mechanism:</strong> Pub/sub invalidation messages across instances.
    /// </para>
    /// <para>
    /// <strong>Stale window:</strong> Milliseconds to seconds (network latency dependent).
    /// </para>
    /// <para>
    /// <strong>Requires:</strong> L2 (Redis) must be enabled for cross-instance invalidation.
    /// </para>
    /// </summary>
    Strong,

    /// <summary>
    /// Eventual consistency: TTL-based cache expiration.
    /// <para>
    /// <strong>Mechanism:</strong> Cache entries expire after configured TTL.
    /// </para>
    /// <para>
    /// <strong>Stale window:</strong> Up to configured TtlSeconds (e.g., 10s, 30s).
    /// </para>
    /// <para>
    /// <strong>Best for:</strong> Development, single-instance, or stale-tolerant data.
    /// </para>
    /// </summary>
    Eventual
}

