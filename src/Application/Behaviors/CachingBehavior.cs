using Application.Interfaces;
using Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that provides transparent caching for queries.
/// Queries implementing ICacheableQuery are automatically cached.
/// Commands implementing ICacheInvalidatingCommand automatically invalidate cache.
/// This makes caching invisible to handlers and other layers.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Handle cacheable queries
        if (request is ICacheableQuery cacheableQuery)
        {
            return await HandleCacheableQuery(cacheableQuery, next);
        }

        // Execute the request
        var response = await next();

        // Handle cache-invalidating commands after successful execution
        if (request is ICacheInvalidatingCommand invalidatingCommand)
        {
            InvalidateCache(invalidatingCommand);
        }

        return response;
    }

    private async Task<TResponse> HandleCacheableQuery(
        ICacheableQuery query,
        RequestHandlerDelegate<TResponse> next)
    {
        if (query.BypassCache)
        {
            _logger.LogDebug("Cache bypassed for query: {CacheKey}", query.CacheKey);
            return await next();
        }

        // Try to get from cache
        if (_cache.TryGet<TResponse>(query.CacheKey, out var cachedResponse) && cachedResponse != null)
        {
            _logger.LogDebug("Cache hit for query: {CacheKey}", query.CacheKey);
            return cachedResponse;
        }

        _logger.LogDebug("Cache miss for query: {CacheKey}", query.CacheKey);

        // Execute the query
        var response = await next();

        // Cache the response
        if (response != null)
        {
            var expiry = query.CacheTtlSeconds.HasValue
                ? TimeSpan.FromSeconds(query.CacheTtlSeconds.Value)
                : (TimeSpan?)null;

            _cache.Set(query.CacheKey, response, expiry);
            _logger.LogDebug("Cached response for query: {CacheKey}", query.CacheKey);
        }

        return response;
    }

    private void InvalidateCache(ICacheInvalidatingCommand command)
    {
        foreach (var key in command.CacheKeysToInvalidate)
        {
            if (key.EndsWith('*'))
            {
                // Pattern-based invalidation (prefix match)
                var prefix = key[..^1]; // Remove trailing '*'
                _cache.RemoveByPrefix(prefix);
                _logger.LogDebug("Invalidated cache keys with prefix: {Prefix}", prefix);
            }
            else
            {
                // Exact key invalidation
                _cache.Remove(key);
                _logger.LogDebug("Invalidated cache key: {CacheKey}", key);
            }
        }
    }
}

