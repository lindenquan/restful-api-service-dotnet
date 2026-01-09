using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Infrastructure.Cache;

/// <summary>
/// ASP.NET Core action filter that handles caching based on [LocalCache] and [RemoteCache] attributes.
/// <para>
/// For GET requests:
/// - Checks cache before executing the action
/// - Caches successful responses (2xx status codes)
/// </para>
/// <para>
/// For write operations (POST, PUT, DELETE):
/// - Acquires lock (for Strong/Serializable consistency)
/// - Invalidates cache after successful operation
/// - Releases lock
/// </para>
/// </summary>
public sealed class CacheActionFilter : IAsyncActionFilter
{
    private readonly LocalCacheService? _localCache;
    private readonly RemoteCacheService? _remoteCache;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<CacheActionFilter> _logger;

    public CacheActionFilter(
        LocalCacheService? localCache,
        RemoteCacheService? remoteCache,
        CacheSettings cacheSettings,
        ILogger<CacheActionFilter> logger)
    {
        _localCache = localCache;
        _remoteCache = remoteCache;
        _cacheSettings = cacheSettings;
        _logger = logger;
    }

    /// <summary>
    /// Gets the effective consistency level, using config default if attribute doesn't specify.
    /// </summary>
    private CacheConsistency GetEffectiveConsistency(RemoteCacheAttribute? attr) =>
        attr?.Consistency ?? _cacheSettings.Remote.Consistency;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Check for cache attributes
        var localCacheAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<LocalCacheAttribute>().FirstOrDefault();
        var remoteCacheAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<RemoteCacheAttribute>().FirstOrDefault();

        // No caching configured - just execute the action
        if (localCacheAttr == null && remoteCacheAttr == null)
        {
            await next();
            return;
        }

        var httpMethod = context.HttpContext.Request.Method;
        var cacheKey = BuildCacheKey(context, localCacheAttr ?? (CacheAttribute)remoteCacheAttr!);

        // Handle based on HTTP method
        if (httpMethod == HttpMethods.Get)
        {
            await HandleGetRequestAsync(context, next, localCacheAttr, remoteCacheAttr, cacheKey);
        }
        else if (httpMethod == HttpMethods.Post ||
                 httpMethod == HttpMethods.Put ||
                 httpMethod == HttpMethods.Patch ||
                 httpMethod == HttpMethods.Delete)
        {
            await HandleWriteRequestAsync(context, next, remoteCacheAttr, cacheKey);
        }
        else
        {
            await next();
        }
    }

    private async Task HandleGetRequestAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next,
        LocalCacheAttribute? localAttr,
        RemoteCacheAttribute? remoteAttr,
        string cacheKey)
    {
        // Try Local cache first
        if (localAttr != null && _localCache != null)
        {
            if (_localCache.TryGet<object>(cacheKey, out var localCached) && localCached != null)
            {
                _logger.LogDebug("Local cache hit for key: {Key}", cacheKey);
                context.Result = new OkObjectResult(localCached);
                return;
            }
        }

        // Try Remote cache
        if (remoteAttr != null && _remoteCache != null)
        {
            var consistency = GetEffectiveConsistency(remoteAttr);
            var result = await _remoteCache.GetWithConsistencyAsync<object>(
                cacheKey,
                consistency,
                context.HttpContext.RequestAborted);

            if (result.Found)
            {
                _logger.LogDebug("Remote cache hit for key: {Key}", cacheKey);
                context.Result = new OkObjectResult(result.Value);
                return;
            }

            if (result.IsLocked)
            {
                _logger.LogDebug("Cache bypassed due to lock for key: {Key}", cacheKey);
                // Continue to execute action (bypass cache)
            }
        }

        // Cache miss - execute action
        var executedContext = await next();

        // Cache successful responses
        if (executedContext.Result is ObjectResult { StatusCode: >= 200 and < 300, Value: not null } objectResult)
        {
            if (localAttr != null && _localCache != null)
            {
                _localCache.Set(cacheKey, objectResult.Value);
                _logger.LogDebug("Cached in Local for key: {Key}", cacheKey);
            }

            if (remoteAttr != null && _remoteCache != null)
            {
                _remoteCache.Set(cacheKey, objectResult.Value, remoteAttr.GetTtl());
                _logger.LogDebug("Cached in Remote for key: {Key}", cacheKey);
            }
        }
    }

    private async Task HandleWriteRequestAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next,
        RemoteCacheAttribute? remoteAttr,
        string cacheKey)
    {
        // Build list of all keys to invalidate (primary key + any additional InvalidateKeys)
        var keysToInvalidate = BuildInvalidationKeys(context, remoteAttr, cacheKey);

        // Track locks acquired (only for exact keys, not prefix patterns)
        var acquiredLocks = new List<(string Key, string Token)>();

        // Acquire locks for Strong/Serializable consistency
        var consistency = GetEffectiveConsistency(remoteAttr);
        if (remoteAttr != null && _remoteCache != null &&
            consistency != CacheConsistency.Eventual)
        {
            foreach (var key in keysToInvalidate.Where(k => !k.IsPrefix))
            {
                var lockToken = _remoteCache.AcquireLock(key.Key);
                if (lockToken != null)
                {
                    acquiredLocks.Add((key.Key, lockToken));
                    _logger.LogDebug("Lock acquired for key: {Key}", key.Key);
                }
                else
                {
                    _logger.LogWarning("Failed to acquire lock for key: {Key}, proceeding without lock", key.Key);
                }
            }
        }

        try
        {
            // Execute the action
            await next();
        }
        finally
        {
            // Invalidate cache and release locks
            if (_remoteCache != null)
            {
                foreach (var key in keysToInvalidate)
                {
                    if (key.IsPrefix)
                    {
                        _remoteCache.RemoveByPrefix(key.Key);
                        _logger.LogDebug("Cache invalidated by prefix: {Prefix}*", key.Key);
                    }
                    else
                    {
                        _remoteCache.Remove(key.Key);
                        _logger.LogDebug("Cache invalidated for key: {Key}", key.Key);
                    }
                }

                // Release all acquired locks
                foreach (var (key, token) in acquiredLocks)
                {
                    _remoteCache.ReleaseLock(key, token);
                    _logger.LogDebug("Lock released for key: {Key}", key);
                }
            }
        }
    }

    /// <summary>
    /// Build list of cache keys to invalidate from the primary key and InvalidateKeys patterns.
    /// </summary>
    private static List<InvalidationKey> BuildInvalidationKeys(
        ActionExecutingContext context,
        RemoteCacheAttribute? remoteAttr,
        string primaryKey)
    {
        var keys = new List<InvalidationKey>
        {
            new(primaryKey, IsPrefix: false)
        };

        if (remoteAttr?.InvalidateKeys is not { Length: > 0 })
            return keys;

        foreach (var pattern in remoteAttr.InvalidateKeys)
        {
            var (resolvedKey, isPrefix) = ResolveKeyPattern(pattern, context);
            if (!string.IsNullOrEmpty(resolvedKey))
            {
                keys.Add(new InvalidationKey(resolvedKey, isPrefix));
            }
        }

        return keys;
    }

    /// <summary>
    /// Resolve a key pattern by substituting route parameters and detecting prefix patterns.
    /// </summary>
    /// <param name="pattern">Pattern like "patients:{id}" or "documents:*"</param>
    /// <param name="context">Action context containing route values</param>
    /// <returns>Resolved key and whether it's a prefix pattern</returns>
    private static (string Key, bool IsPrefix) ResolveKeyPattern(string pattern, ActionExecutingContext context)
    {
        var isPrefix = pattern.EndsWith('*');
        var keyPattern = isPrefix ? pattern[..^1].TrimEnd(':') : pattern;

        // Replace {paramName} placeholders with actual route/action values
        var resolvedKey = Regex.Replace(keyPattern, @"\{(\w+)\}", match =>
        {
            var paramName = match.Groups[1].Value;

            // Try route values first
            if (context.RouteData.Values.TryGetValue(paramName, out var routeValue) && routeValue != null)
            {
                return routeValue.ToString() ?? string.Empty;
            }

            // Try action arguments
            if (context.ActionArguments.TryGetValue(paramName, out var argValue) && argValue != null)
            {
                return argValue.ToString() ?? string.Empty;
            }

            // Parameter not found - return empty (will be filtered out)
            return string.Empty;
        });

        return (resolvedKey, isPrefix);
    }

    /// <summary>
    /// Represents a cache key to invalidate.
    /// </summary>
    private readonly record struct InvalidationKey(string Key, bool IsPrefix);

    /// <summary>
    /// Build cache key from route template and parameters.
    /// Format: {prefix}:{routeValues} e.g., "patients:123" or "documents:456"
    /// </summary>
    private static string BuildCacheKey(ActionExecutingContext context, CacheAttribute cacheAttr)
    {
        var sb = new StringBuilder();

        // Use custom prefix if specified, otherwise derive from controller/action
        if (!string.IsNullOrEmpty(cacheAttr.KeyPrefix))
        {
            sb.Append(cacheAttr.KeyPrefix);
        }
        else
        {
            // Use controller name as prefix
            var controllerName = context.RouteData.Values["controller"]?.ToString()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(controllerName))
            {
                sb.Append(controllerName);
            }
        }

        // Append route values (e.g., id)
        foreach (var routeValue in context.RouteData.Values)
        {
            if (routeValue.Key is "controller" or "action")
                continue;

            if (sb.Length > 0)
                sb.Append(':');

            sb.Append(routeValue.Value);
        }

        return sb.ToString();
    }
}

