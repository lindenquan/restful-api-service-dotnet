namespace Application.Interfaces;

/// <summary>
/// Interface to mark a query as cacheable.
/// Implement this interface on MediatR queries to enable automatic caching.
/// The cache is invisible to handlers - the CachingBehavior handles it.
/// </summary>
public interface ICacheableQuery
{
    /// <summary>
    /// The cache key for this query.
    /// Should uniquely identify the query and its parameters.
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Optional TTL override in seconds. If null, uses default from configuration.
    /// </summary>
    int? CacheTtlSeconds => null;

    /// <summary>
    /// Whether to bypass cache and always fetch fresh data.
    /// Useful for testing or when stale data is unacceptable.
    /// </summary>
    bool BypassCache => false;
}

/// <summary>
/// Interface to mark a command that should invalidate cache entries.
/// Implement this on commands that modify data to automatically clear related cache.
/// </summary>
public interface ICacheInvalidatingCommand
{
    /// <summary>
    /// Cache keys or key patterns to invalidate when this command executes.
    /// Patterns ending with '*' will be treated as prefixes.
    /// </summary>
    IEnumerable<string> CacheKeysToInvalidate { get; }
}

