using Adapters.Persistence.Security;
using Application.Interfaces.Repositories;
using Entities;
using MongoDB.Driver;

namespace Adapters.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IUserRepository.
/// API keys are stored as SHA-256 hashes.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoUserRepository : MongoRepository<User>, IUserRepository
{
    public MongoUserRepository(IMongoCollection<User> collection)
        : base(collection)
    {
    }

    /// <summary>
    /// Get user by API key (hashes the key and looks up by hash).
    /// </summary>
    public async Task<User?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        var hash = ApiKeyHasher.HashApiKey(apiKey);
        return await GetByApiKeyHashAsync(hash, ct);
    }

    /// <summary>
    /// Get user by API key hash directly.
    /// </summary>
    public async Task<User?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken ct = default)
    {
        return await _collection
            .Find(u => u.ApiKeyHash == apiKeyHash && !u.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _collection
            .Find(u => u.Email == email && !u.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        return await _collection
            .Find(u => u.UserName == userName && !u.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken ct = default)
    {
        return await _collection
            .Find(u => u.IsActive && !u.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<bool> IsApiKeyValidAsync(string apiKey, CancellationToken ct = default)
    {
        var hash = ApiKeyHasher.HashApiKey(apiKey);
        return await _collection
            .Find(u => u.ApiKeyHash == hash && u.IsActive && !u.IsDeleted)
            .AnyAsync(ct);
    }

    public async Task UpdateLastUsedAsync(string apiKeyHash, CancellationToken ct = default)
    {
        var filter = Builders<User>.Filter.Eq(u => u.ApiKeyHash, apiKeyHash);
        var update = Builders<User>.Update.Set(u => u.LastUsedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}

