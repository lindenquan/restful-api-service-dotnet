namespace Application.Interfaces.Services;

public interface ICacheService
{
    void Set<T>(string key, T value, TimeSpan? expiry = null);
    T? Get<T>(string key);
    bool TryGet<T>(string key, out T? value);
    void Remove(string key);

    /// <summary>
    /// Remove all cache entries matching the given prefix pattern.
    /// Pattern should end with '*' (e.g., "orders:paged:*").
    /// </summary>
    void RemoveByPrefix(string prefix);

    bool Exists(string key);
    T GetOrAdd<T>(string key, Func<T> factory, TimeSpan? expiry = null);
    Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
}

