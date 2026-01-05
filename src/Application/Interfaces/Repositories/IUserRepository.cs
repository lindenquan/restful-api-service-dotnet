using Domain;

namespace Application.Interfaces.Repositories;

/// <summary>
/// Repository interface for API users.
/// Note: API keys are stored as SHA-256 hashes. The repository handles
/// hashing the provided key before lookup.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Get user by API key (hashes the key and looks up by hash).
    /// </summary>
    /// <param name="apiKey">Plain-text API key to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>User if found and key matches, null otherwise</returns>
    Task<User?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default);

    /// <summary>
    /// Get user by API key hash directly (for internal use).
    /// </summary>
    /// <param name="apiKeyHash">SHA-256 hash of the API key</param>
    /// <param name="ct">Cancellation token</param>
    Task<User?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken ct = default);

    /// <summary>
    /// Get user by email.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Get user by username.
    /// </summary>
    Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default);

    /// <summary>
    /// Get all active API users.
    /// </summary>
    Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken ct = default);

    /// <summary>
    /// Check if an API key exists and is active.
    /// </summary>
    /// <param name="apiKey">Plain-text API key to validate</param>
    /// <param name="ct">Cancellation token</param>
    Task<bool> IsApiKeyValidAsync(string apiKey, CancellationToken ct = default);

    /// <summary>
    /// Update last used timestamp for an API key.
    /// </summary>
    /// <param name="apiKeyHash">SHA-256 hash of the API key</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateLastUsedAsync(string apiKeyHash, CancellationToken ct = default);
}

