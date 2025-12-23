namespace Application.Interfaces.Services;

public interface ICacheService
{
    void Set<T>(string key, T value, TimeSpan? expiry = null);
    T? Get<T>(string key);
    bool TryGet<T>(string key, out T? value);
    void Remove(string key);
    bool Exists(string key);
    T GetOrAdd<T>(string key, Func<T> factory, TimeSpan? expiry = null);
    Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
}

