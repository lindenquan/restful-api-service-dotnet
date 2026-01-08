using System.Linq.Expressions;
using Application.Interfaces.Repositories;
using Domain;
using DTOs.Shared;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic MongoDB repository implementation with resilience (retry + circuit breaker).
/// Works with data models internally, maps to/from domain entities at boundaries.
/// Uses UUID v7 for entity identifiers.
/// </summary>
/// <typeparam name="TEntity">Domain entity type</typeparam>
/// <typeparam name="TDataModel">Data model type for persistence</typeparam>
public abstract class MongoRepository<TEntity, TDataModel> : IRepository<TEntity>
    where TEntity : BaseEntity
    where TDataModel : BaseDataModel
{
    protected readonly IMongoCollection<TDataModel> _collection;
    protected readonly IResilientExecutor _resilientExecutor;

    protected MongoRepository(IMongoCollection<TDataModel> collection, IResilientExecutor resilientExecutor)
    {
        _collection = collection;
        _resilientExecutor = resilientExecutor;
    }

    /// <summary>
    /// Map a data model to a domain entity.
    /// </summary>
    protected abstract TEntity ToDomain(TDataModel model);

    /// <summary>
    /// Map a domain entity to a data model.
    /// </summary>
    protected abstract TDataModel ToDataModel(TEntity entity);

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.And(
                Builders<TDataModel>.Filter.Eq(e => e.Id, id),
                Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false));
            var model = await _collection.Find(filter).FirstOrDefaultAsync(token);
            return model == null ? null : ToDomain(model);
        }, ct);
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var models = await _collection.Find(e => !e.Metadata.IsDeleted).ToListAsync(token);
            return models.Select(ToDomain);
        }, ct);
    }

    public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        // For find operations, we need to fetch all non-deleted and filter in memory
        // This is a limitation of mapping between entity and data model
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var models = await _collection.Find(e => !e.Metadata.IsDeleted).ToListAsync(token);
            var entities = models.Select(ToDomain);
            return entities.Where(predicate.Compile());
        }, ct);
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var models = await _collection.Find(e => !e.Metadata.IsDeleted).ToListAsync(token);
            var entities = models.Select(ToDomain);
            return entities.FirstOrDefault(predicate.Compile());
        }, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var models = await _collection.Find(e => !e.Metadata.IsDeleted).ToListAsync(token);
            var entities = models.Select(ToDomain);
            return entities.Any(predicate.Compile());
        }, ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
            (int)await _collection.CountDocumentsAsync(e => !e.Metadata.IsDeleted, cancellationToken: token), ct);
    }

    public async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var models = await _collection.Find(e => !e.Metadata.IsDeleted).ToListAsync(token);
            var entities = models.Select(ToDomain);
            return entities.Count(predicate.Compile());
        }, ct);
    }

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            // Generate UUID v7 if not already set
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.CreateVersion7();
            }
            var dataModel = ToDataModel(entity);
            dataModel.Id = entity.Id;
            dataModel.Metadata.CreatedAt = DateTime.UtcNow;
            dataModel.Metadata.CreatedBy = entity.CreatedBy;
            await _collection.InsertOneAsync(dataModel, cancellationToken: token);
        }, ct);
    }

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var dataModels = new List<TDataModel>();

            foreach (var entity in entities)
            {
                // Generate UUID v7 if not already set
                if (entity.Id == Guid.Empty)
                {
                    entity.Id = Guid.CreateVersion7();
                }
                var dataModel = ToDataModel(entity);
                dataModel.Id = entity.Id;
                dataModel.Metadata.CreatedAt = DateTime.UtcNow;
                dataModel.Metadata.CreatedBy = entity.CreatedBy;
                dataModels.Add(dataModel);
            }

            await _collection.InsertManyAsync(dataModels, cancellationToken: token);
        }, ct);
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var dataModel = ToDataModel(entity);
            dataModel.Metadata.UpdatedAt = DateTime.UtcNow;
            dataModel.Metadata.UpdatedBy = entity.UpdatedBy;
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Id, entity.Id);
            await _collection.ReplaceOneAsync(filter, dataModel, cancellationToken: token);
        }, ct);
    }

    public async Task SoftDeleteAsync(TEntity entity, Guid? deletedBy = null, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Id, entity.Id);
            var update = Builders<TDataModel>.Update
                .Set(e => e.Metadata.IsDeleted, true)
                .Set(e => e.Metadata.DeletedAt, DateTime.UtcNow)
                .Set(e => e.Metadata.DeletedBy, deletedBy);
            await _collection.UpdateOneAsync(filter, update, cancellationToken: token);
        }, ct);
    }

    public async Task HardDeleteAsync(TEntity entity, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Id, entity.Id);
            await _collection.DeleteOneAsync(filter, cancellationToken: token);
        }, ct);
    }

    public async Task HardDeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var ids = entities.Select(e => e.Id).ToList();
            var filter = Builders<TDataModel>.Filter.In(e => e.Id, ids);
            await _collection.DeleteManyAsync(filter, cancellationToken: token);
        }, ct);
    }

    // Pagination implementation
    public async Task<PagedData<TEntity>> GetPagedAsync(
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);

            var query = _collection.Find(filter);
            query = ApplyOrdering(query, orderBy, descending);

            var items = await query
                .Skip(skip)
                .Limit(top)
                .ToListAsync(token);

            long totalCount = 0;
            if (includeCount)
            {
                totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: token);
            }

            return new PagedData<TEntity>(
                items.Select(ToDomain).ToList(),
                totalCount);
        }, ct);
    }

    public async Task<PagedData<TEntity>> GetPagedAsync(
        Expression<Func<TEntity, bool>> predicate,
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default)
    {
        // For predicate-based pagination, we need to filter in memory after fetching
        // This is a limitation of mapping between entity and data model
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);
            var allModels = await _collection.Find(filter).ToListAsync(token);

            var allEntities = allModels.Select(ToDomain).ToList();
            var filteredEntities = allEntities.Where(predicate.Compile()).ToList();

            // Apply ordering if specified
            if (!string.IsNullOrEmpty(orderBy))
            {
                filteredEntities = ApplyOrderingInMemory(filteredEntities, orderBy, descending);
            }

            var items = filteredEntities.Skip(skip).Take(top).ToList();
            var totalCount = includeCount ? filteredEntities.Count : 0;

            return new PagedData<TEntity>(items, totalCount);
        }, ct);
    }

    /// <summary>
    /// Apply ordering to a MongoDB query.
    /// Default ordering is by Metadata.CreatedAt descending (newest first).
    /// </summary>
    protected virtual IFindFluent<TDataModel, TDataModel> ApplyOrdering(
        IFindFluent<TDataModel, TDataModel> query,
        string? orderBy,
        bool descending)
    {
        // Default ordering by CreatedAt descending if no orderBy specified
        if (string.IsNullOrEmpty(orderBy))
        {
            return query.SortByDescending(e => e.Metadata.CreatedAt);
        }

        // For now, support common orderings - subclasses can override for entity-specific fields
        var normalizedOrderBy = orderBy.ToLowerInvariant();

        return normalizedOrderBy switch
        {
            "createdat" => descending
                ? query.SortByDescending(e => e.Metadata.CreatedAt)
                : query.SortBy(e => e.Metadata.CreatedAt),
            "updatedat" => descending
                ? query.SortByDescending(e => e.Metadata.UpdatedAt)
                : query.SortBy(e => e.Metadata.UpdatedAt),
            "id" => descending
                ? query.SortByDescending(e => e.Id)
                : query.SortBy(e => e.Id),
            _ => query.SortByDescending(e => e.Metadata.CreatedAt) // Fallback to default
        };
    }

    /// <summary>
    /// Apply ordering to an in-memory collection.
    /// </summary>
    protected virtual List<TEntity> ApplyOrderingInMemory(
        List<TEntity> entities,
        string orderBy,
        bool descending)
    {
        var normalizedOrderBy = orderBy.ToLowerInvariant();

        return normalizedOrderBy switch
        {
            "createdat" => descending
                ? entities.OrderByDescending(e => e.CreatedAt).ToList()
                : entities.OrderBy(e => e.CreatedAt).ToList(),
            "updatedat" => descending
                ? entities.OrderByDescending(e => e.UpdatedAt).ToList()
                : entities.OrderBy(e => e.UpdatedAt).ToList(),
            "id" => descending
                ? entities.OrderByDescending(e => e.Id).ToList()
                : entities.OrderBy(e => e.Id).ToList(),
            _ => entities // Return as-is if unknown field
        };
    }
}

