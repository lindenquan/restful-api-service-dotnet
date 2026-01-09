namespace Infrastructure.Cache;

/// <summary>
/// Base attribute for cache configuration on controller endpoints.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public abstract class CacheAttribute : Attribute
{
    /// <summary>
    /// Optional custom key prefix. If not set, uses route pattern (e.g., "patients:{id}").
    /// </summary>
    public string? KeyPrefix { get; set; }
}

/// <summary>
/// Marks an endpoint to use Local (in-memory) cache.
/// <para>
/// <strong>Use only for static reference data</strong> that never changes during app runtime
/// (e.g., drug lists, ICD codes, configuration lookups).
/// </para>
/// <para>
/// Local cache has infinite TTL - entries are only evicted on app restart or LRU eviction.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [HttpGet("{id}")]
/// [LocalCache]
/// public async Task&lt;ActionResult&lt;DocumentDto&gt;&gt; GetDocument(int id) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class LocalCacheAttribute : CacheAttribute
{
}

/// <summary>
/// Marks an endpoint to use Remote (Redis) cache.
/// <para>
/// Uses the default consistency level from configuration (Cache:Remote:Consistency).
/// Optionally override consistency per-endpoint:
/// <list type="bullet">
/// <item><term>Eventual</term><description>TTL-based, stale OK for duration of TTL</description></item>
/// <item><term>Strong</term><description>Lock-based, readers bypass to DB when locked</description></item>
/// <item><term>Serializable</term><description>Lock-based, readers wait for lock release</description></item>
/// </list>
/// </para>
/// <para>
/// For write operations that affect multiple cache keys (e.g., transactions updating multiple entities),
/// use <see cref="InvalidateKeys"/> to specify additional keys to lock and invalidate.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Use default consistency from config
/// [HttpGet("{id}")]
/// [RemoteCache]
/// public async Task&lt;ActionResult&lt;PatientDto&gt;&gt; GetPatient(int id) { ... }
///
/// // Override consistency for specific endpoint
/// [HttpGet("{id}")]
/// [RemoteCache(CacheConsistency.Serializable)]
/// public async Task&lt;ActionResult&lt;TransactionDto&gt;&gt; GetTransaction(int id) { ... }
///
/// // Invalidate multiple cache keys (for transactions)
/// [HttpPut("{id}")]
/// [RemoteCache(InvalidateKeys = ["patients:{id}", "documents:{id}:*"])]
/// public async Task&lt;ActionResult&gt; UpdatePatientWithDocuments(int id) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RemoteCacheAttribute : CacheAttribute
{
    /// <summary>
    /// Value indicating no TTL override (use configuration default).
    /// </summary>
    public const int DefaultTtl = -1;

    /// <summary>
    /// The consistency level for this cached endpoint, or null to use configuration default.
    /// </summary>
    public CacheConsistency? Consistency { get; }

    /// <summary>
    /// TTL override in seconds. Set to -1 (default) to use configuration default.
    /// </summary>
    public int TtlSeconds { get; set; } = DefaultTtl;

    /// <summary>
    /// Additional cache keys to lock and invalidate on write operations (POST, PUT, PATCH, DELETE).
    /// <para>
    /// Supports route parameter substitution using {paramName} syntax.
    /// Use * suffix for prefix-based invalidation (no locking, just invalidation).
    /// </para>
    /// <para>
    /// Examples:
    /// <list type="bullet">
    /// <item><term>"patients:{id}"</term><description>Exact key - locks and invalidates specific patient</description></item>
    /// <item><term>"documents:*"</term><description>Prefix - invalidates all documents (no lock)</description></item>
    /// <item><term>"documents:{patientId}:*"</term><description>Prefix with param - invalidates all docs for patient</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public string[] InvalidateKeys { get; set; } = [];

    /// <summary>
    /// Gets the TTL as a TimeSpan, or null if using default.
    /// </summary>
    public TimeSpan? GetTtl() => TtlSeconds == DefaultTtl ? null : TimeSpan.FromSeconds(TtlSeconds);

    /// <summary>
    /// Creates a RemoteCache attribute using default consistency from configuration.
    /// </summary>
    public RemoteCacheAttribute()
    {
        Consistency = null; // Use config default
    }

    /// <summary>
    /// Creates a RemoteCache attribute with the specified consistency level (overrides config).
    /// </summary>
    /// <param name="consistency">The consistency level for cache operations.</param>
    public RemoteCacheAttribute(CacheConsistency consistency)
    {
        Consistency = consistency;
    }
}

