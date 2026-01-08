using Application.Interfaces.Services;

namespace Infrastructure.Cache;

/// <summary>
/// No-op cache service implementation used when caching is disabled.
/// All operations are pass-through with no caching.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class NullCacheService : ICacheService
{
    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        // No-op
    }

    public T? Get<T>(string key) => default;

    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        return false;
    }

    public void Remove(string key)
    {
        // No-op
    }

    public void RemoveByPrefix(string prefix)
    {
        // No-op
    }

    public bool Exists(string key) => false;

    public T GetOrAdd<T>(string key, Func<T> factory, TimeSpan? expiry = null) => factory();

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        => await factory();
}

