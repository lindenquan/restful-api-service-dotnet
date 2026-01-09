using System.Linq.Expressions;
using Domain;
using Infrastructure.Persistence.Models;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Partial class containing query and find operations.
/// </summary>
public abstract partial class MongoRepository<TEntity, TDataModel>
    where TEntity : BaseEntity
    where TDataModel : BaseDataModel
{
    public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        // For find operations, we need to fetch all non-deleted and filter in memory
        // This is a limitation of mapping between entity and data model
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);
            var models = Session != null
                ? await _collection.Find(Session, filter).ToListAsync(token)
                : await _collection.Find(filter).ToListAsync(token);

            var entities = models.Select(ToDomain);
            return entities.Where(predicate.Compile());
        }, ct);
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);
            var models = Session != null
                ? await _collection.Find(Session, filter).ToListAsync(token)
                : await _collection.Find(filter).ToListAsync(token);

            var entities = models.Select(ToDomain);
            return entities.FirstOrDefault(predicate.Compile());
        }, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);
            var models = Session != null
                ? await _collection.Find(Session, filter).ToListAsync(token)
                : await _collection.Find(filter).ToListAsync(token);

            var entities = models.Select(ToDomain);
            return entities.Any(predicate.Compile());
        }, ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);
            var count = Session != null
                ? await _collection.CountDocumentsAsync(Session, filter, cancellationToken: token)
                : await _collection.CountDocumentsAsync(filter, cancellationToken: token);

            return (int)count;
        }, ct);
    }

    public async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);
            var models = Session != null
                ? await _collection.Find(Session, filter).ToListAsync(token)
                : await _collection.Find(filter).ToListAsync(token);

            var entities = models.Select(ToDomain);
            return entities.Count(predicate.Compile());
        }, ct);
    }
}

