using System.Linq.Expressions;
using Application.Interfaces.Repositories;
using Domain;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic MongoDB repository implementation.
/// </summary>
/// <typeparam name="T">Entity type that has an Id property</typeparam>
public class MongoRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly IMongoCollection<T> _collection;

    public MongoRepository(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        // Exclude soft-deleted entities
        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(e => e.Id, id),
            Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
    {
        // Exclude soft-deleted entities
        return await _collection.Find(e => !e.Metadata.IsDeleted).ToListAsync(ct);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        // Combine with soft-delete filter
        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Where(predicate),
            Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
        return await _collection.Find(filter).ToListAsync(ct);
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Where(predicate),
            Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Where(predicate),
            Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
        return await _collection.Find(filter).AnyAsync(ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return (int)await _collection.CountDocumentsAsync(e => !e.Metadata.IsDeleted, cancellationToken: ct);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Where(predicate),
            Builders<T>.Filter.Eq(e => e.Metadata.IsDeleted, false));
        return (int)await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        // Auto-generate ID if not set
        if (entity.Id == 0)
        {
            entity.Id = await GetNextIdAsync(ct);
        }
        entity.Metadata.CreatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        var entityList = entities.ToList();
        var nextId = await GetNextIdAsync(ct);

        foreach (var entity in entityList)
        {
            if (entity.Id == 0)
            {
                entity.Id = nextId++;
            }
            entity.Metadata.CreatedAt = DateTime.UtcNow;
        }

        await _collection.InsertManyAsync(entityList, cancellationToken: ct);
    }

    public void Update(T entity)
    {
        entity.Metadata.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<T>.Filter.Eq(e => e.Id, entity.Id);
        // Note: MongoDB driver doesn't support synchronous operations well,
        // so we use GetAwaiter().GetResult() here. In production, use async.
        _collection.ReplaceOneAsync(filter, entity).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Soft delete - marks the entity as deleted without removing from database.
    /// </summary>
    public void SoftDelete(T entity, string? deletedBy = null)
    {
        entity.Metadata.IsDeleted = true;
        entity.Metadata.DeletedAt = DateTime.UtcNow;
        entity.Metadata.DeletedBy = deletedBy;
        Update(entity);
    }

    /// <summary>
    /// Hard delete - permanently removes the entity from the database.
    /// </summary>
    public void HardDelete(T entity)
    {
        var filter = Builders<T>.Filter.Eq(e => e.Id, entity.Id);
        _collection.DeleteOneAsync(filter).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Hard delete multiple entities permanently.
    /// </summary>
    public void HardDeleteRange(IEnumerable<T> entities)
    {
        var ids = entities.Select(e => e.Id).ToList();
        var filter = Builders<T>.Filter.In(e => e.Id, ids);
        _collection.DeleteManyAsync(filter).GetAwaiter().GetResult();
    }

    // Legacy methods - kept for backward compatibility
    [Obsolete("Use SoftDelete or HardDelete instead")]
    public void Remove(T entity)
    {
        var filter = Builders<T>.Filter.Eq(e => e.Id, entity.Id);
        _collection.DeleteOneAsync(filter).GetAwaiter().GetResult();
    }

    [Obsolete("Use HardDeleteRange instead")]
    public void RemoveRange(IEnumerable<T> entities)
    {
        var ids = entities.Select(e => e.Id).ToList();
        var filter = Builders<T>.Filter.In(e => e.Id, ids);
        _collection.DeleteManyAsync(filter).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the next available ID for auto-increment simulation.
    /// </summary>
    private async Task<int> GetNextIdAsync(CancellationToken ct)
    {
        var lastEntity = await _collection
            .Find(_ => true)
            .SortByDescending(e => e.Id)
            .FirstOrDefaultAsync(ct);

        return (lastEntity?.Id ?? 0) + 1;
    }
}

