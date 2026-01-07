using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Infrastructure.Persistence.Security;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IUserRepository.
/// API keys are stored as SHA-256 hashes.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoUserRepository : MongoRepository<User, UserDataModel>, IUserRepository
{
    public MongoUserRepository(IMongoCollection<UserDataModel> collection, IResilientExecutor resilientExecutor)
        : base(collection, resilientExecutor)
    {
    }

    protected override User ToDomain(UserDataModel model) => UserPersistenceMapper.ToDomain(model);
    protected override UserDataModel ToDataModel(User entity) => UserPersistenceMapper.ToDataModel(entity);

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
        var model = await _collection
            .Find(u => u.ApiKeyHash == apiKeyHash && !u.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);
        return model == null ? null : ToDomain(model);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var model = await _collection
            .Find(u => u.Email == email && !u.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);
        return model == null ? null : ToDomain(model);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        var model = await _collection
            .Find(u => u.UserName == userName && !u.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);
        return model == null ? null : ToDomain(model);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken ct = default)
    {
        var models = await _collection
            .Find(u => u.IsActive && !u.Metadata.IsDeleted)
            .ToListAsync(ct);
        return models.Select(ToDomain);
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
        var filter = Builders<UserDataModel>.Filter.Eq(u => u.ApiKeyHash, apiKeyHash);
        var update = Builders<UserDataModel>.Update.Set(u => u.LastUsedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}

