using System.Linq.Expressions;
using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic MongoDB repository implementation with resilience (retry + circuit breaker).
/// </summary>
/// <typeparam name="T">Entity type that has an Id property</typeparam>
public class MongoRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly IMongoCollection<T> _collection;
    protected readonly IResilientExecutor _resilientExecutor;

    public MongoRepository(IMongoCollection<T> collection, IResilientExecutor resilientExecutor)
    {
        _collection = collection;
        _resilientExecutor = resilientExecutor;
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Eq(e => e.Id, id),
                Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
            return await _collection.Find(filter).FirstOrDefaultAsync(token);
        }, ct);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
            await _collection.Find(e => !e.Metadata.IsDeleted).ToListAsync(token), ct);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Where(predicate),
                Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
            return await _collection.Find(filter).ToListAsync(token);
        }, ct);
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Where(predicate),
                Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
            return await _collection.Find(filter).FirstOrDefaultAsync(token);
        }, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Where(predicate),
                Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
            return await _collection.Find(filter).AnyAsync(token);
        }, ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
            (int)await _collection.CountDocumentsAsync(e => !e.Metadata.IsDeleted, cancellationToken: token), ct);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Where(predicate),
                Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
            return (int)await _collection.CountDocumentsAsync(filter, cancellationToken: token);
        }, ct);
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            if (entity.Id == 0)
            {
                entity.Id = await GetNextIdAsync(token);
            }
            entity.Metadata.CreatedAt = DateTime.UtcNow;
            await _collection.InsertOneAsync(entity, cancellationToken: token);
        }, ct);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var entityList = entities.ToList();
            var nextId = await GetNextIdAsync(token);

            foreach (var entity in entityList)
            {
                if (entity.Id == 0)
                {
                    entity.Id = nextId++;
                }
                entity.Metadata.CreatedAt = DateTime.UtcNow;
            }

            await _collection.InsertManyAsync(entityList, cancellationToken: token);
        }, ct);
    }

    public void Update(T entity)
    {
        entity.Metadata.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<T>.Filter.Eq(e => e.Id, entity.Id);
        _collection.ReplaceOneAsync(filter, entity).GetAwaiter().GetResult();
    }

    public void SoftDelete(T entity, string? deletedBy = null)
    {
        entity.Metadata.IsDeleted = true;
        entity.Metadata.DeletedAt = DateTime.UtcNow;
        entity.Metadata.DeletedBy = deletedBy;
        Update(entity);
    }

    public void HardDelete(T entity)
    {
        var filter = Builders<T>.Filter.Eq(e => e.Id, entity.Id);
        _collection.DeleteOneAsync(filter).GetAwaiter().GetResult();
    }

    public void HardDeleteRange(IEnumerable<T> entities)
    {
        var ids = entities.Select(e => e.Id).ToList();
        var filter = Builders<T>.Filter.In(e => e.Id, ids);
        _collection.DeleteManyAsync(filter).GetAwaiter().GetResult();
    }

    public void Remove(T entity)
    {
        HardDelete(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        HardDeleteRange(entities);
    }

    private async Task<int> GetNextIdAsync(CancellationToken ct)
    {
        var lastEntity = await _collection.Find(_ => true).SortByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        return (lastEntity?.Id ?? 0) + 1;
    }
}

