using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Infrastructure.Persistence.Security;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IUserRepository with transaction support.
/// API keys are stored as SHA-256 hashes.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoUserRepository : MongoRepository<User, UserDataModel>, IUserRepository
{
    public MongoUserRepository(
        IMongoCollection<UserDataModel> collection,
        IResilientExecutor resilientExecutor,
        IMongoSessionProvider? sessionProvider = null)
        : base(collection, resilientExecutor, sessionProvider)
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
        var filter = Builders<UserDataModel>.Filter.And(
            Builders<UserDataModel>.Filter.Eq(u => u.ApiKeyHash, apiKeyHash),
            Builders<UserDataModel>.Filter.Eq(u => u.Metadata.IsDeleted, false));

        var model = Session != null
            ? await _collection.Find(Session, filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(filter).FirstOrDefaultAsync(ct);

        return model == null ? null : ToDomain(model);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var filter = Builders<UserDataModel>.Filter.And(
            Builders<UserDataModel>.Filter.Eq(u => u.Email, email),
            Builders<UserDataModel>.Filter.Eq(u => u.Metadata.IsDeleted, false));

        var model = Session != null
            ? await _collection.Find(Session, filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(filter).FirstOrDefaultAsync(ct);

        return model == null ? null : ToDomain(model);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        var filter = Builders<UserDataModel>.Filter.And(
            Builders<UserDataModel>.Filter.Eq(u => u.UserName, userName),
            Builders<UserDataModel>.Filter.Eq(u => u.Metadata.IsDeleted, false));

        var model = Session != null
            ? await _collection.Find(Session, filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(filter).FirstOrDefaultAsync(ct);

        return model == null ? null : ToDomain(model);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken ct = default)
    {
        var filter = Builders<UserDataModel>.Filter.And(
            Builders<UserDataModel>.Filter.Eq(u => u.IsActive, true),
            Builders<UserDataModel>.Filter.Eq(u => u.Metadata.IsDeleted, false));

        var models = Session != null
            ? await _collection.Find(Session, filter).ToListAsync(ct)
            : await _collection.Find(filter).ToListAsync(ct);

        return models.Select(ToDomain);
    }

    public async Task<bool> IsApiKeyValidAsync(string apiKey, CancellationToken ct = default)
    {
        var hash = ApiKeyHasher.HashApiKey(apiKey);
        var filter = Builders<UserDataModel>.Filter.And(
            Builders<UserDataModel>.Filter.Eq(u => u.ApiKeyHash, hash),
            Builders<UserDataModel>.Filter.Eq(u => u.IsActive, true),
            Builders<UserDataModel>.Filter.Eq(u => u.Metadata.IsDeleted, false));

        return Session != null
            ? await _collection.Find(Session, filter).AnyAsync(ct)
            : await _collection.Find(filter).AnyAsync(ct);
    }

    public async Task UpdateLastUsedAsync(string apiKeyHash, CancellationToken ct = default)
    {
        var filter = Builders<UserDataModel>.Filter.Eq(u => u.ApiKeyHash, apiKeyHash);
        var update = Builders<UserDataModel>.Update.Set(u => u.LastUsedAt, DateTime.UtcNow);

        if (Session != null)
            await _collection.UpdateOneAsync(Session, filter, update, cancellationToken: ct);
        else
            await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}

