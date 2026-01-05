using Infrastructure.Persistence.Security;
using Infrastructure.Resilience;
using Application.Interfaces.Repositories;
using Domain;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IUserRepository.
/// API keys are stored as SHA-256 hashes.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoUserRepository : MongoRepository<User>, IUserRepository
{
    public MongoUserRepository(IMongoCollection<User> collection, IResilientExecutor resilientExecutor)
        : base(collection, resilientExecutor)
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
            .Find(u => u.ApiKeyHash == apiKeyHash && !u.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _collection
            .Find(u => u.Email == email && !u.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        return await _collection
            .Find(u => u.UserName == userName && !u.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken ct = default)
    {
        return await _collection
            .Find(u => u.IsActive && !u.Metadata.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<bool> IsApiKeyValidAsync(string apiKey, CancellationToken ct = default)
    {
        var hash = ApiKeyHasher.HashApiKey(apiKey);
        return await _collection
            .Find(u => u.ApiKeyHash == hash && u.IsActive && !u.Metadata.IsDeleted)
            .AnyAsync(ct);
    }

    public async Task UpdateLastUsedAsync(string apiKeyHash, CancellationToken ct = default)
    {
        var filter = Builders<User>.Filter.Eq(u => u.ApiKeyHash, apiKeyHash);
        var update = Builders<User>.Update.Set(u => u.LastUsedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}

