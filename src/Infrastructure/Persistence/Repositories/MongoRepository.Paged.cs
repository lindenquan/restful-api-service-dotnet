using System.Linq.Expressions;
using Domain;
using DTOs.Shared;
using Infrastructure.Persistence.Models;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Partial class containing pagination and ordering operations.
/// </summary>
public abstract partial class MongoRepository<TEntity, TDataModel>
    where TEntity : BaseEntity
    where TDataModel : BaseDataModel
{
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

            var query = Session != null
                ? _collection.Find(Session, filter)
                : _collection.Find(filter);
            query = ApplyOrdering(query, orderBy, descending);

            var items = await query
                .Skip(skip)
                .Limit(top)
                .ToListAsync(token);

            long totalCount = 0;
            if (includeCount)
            {
                totalCount = Session != null
                    ? await _collection.CountDocumentsAsync(Session, filter, cancellationToken: token)
                    : await _collection.CountDocumentsAsync(filter, cancellationToken: token);
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
            var allModels = Session != null
                ? await _collection.Find(Session, filter).ToListAsync(token)
                : await _collection.Find(filter).ToListAsync(token);

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

