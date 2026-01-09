using Application.Interfaces.Services;

namespace Infrastructure.Cache;

/// <summary>
/// No-op cache service implementation for when caching is disabled.
/// All operations are no-ops and return default values.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class NullCacheService : ICacheService
{
    public void Set<T>(string key, T value, TimeSpan? expiry = null) { }

    public T? Get<T>(string key) => default;

    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        return false;
    }

    public void Remove(string key) { }

    public void RemoveByPrefix(string prefix) { }

    public bool Exists(string key) => false;

    public T GetOrAdd<T>(string key, Func<T> factory, TimeSpan? expiry = null) => factory();

    public Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null) => factory();
}
