using System.Linq.Expressions;
using DTOs.Shared;

namespace Application.Interfaces.Repositories;

/// <summary>
/// Generic repository interface for CRUD operations.
/// Uses Guid (UUID v7) for entity identifiers.
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IRepository<T> where T : class
{
    // Query (excludes soft-deleted by default)
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    // Pagination (OData-style)
    /// <summary>
    /// Gets a paged list of entities.
    /// </summary>
    /// <param name="skip">Number of items to skip (offset).</param>
    /// <param name="top">Number of items to take (page size).</param>
    /// <param name="includeCount">Whether to include total count.</param>
    /// <param name="orderBy">Property name to order by (null for default order).</param>
    /// <param name="descending">Whether to order descending.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged data with items and optional total count.</returns>
    Task<PagedData<T>> GetPagedAsync(
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a paged list of entities matching a predicate.
    /// </summary>
    Task<PagedData<T>> GetPagedAsync(
        Expression<Func<T, bool>> predicate,
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default);

    // Commands
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    void Update(T entity);

    /// <summary>
    /// Soft delete - marks the entity as deleted without removing from database.
    /// </summary>
    /// <param name="entity">Entity to soft delete</param>
    /// <param name="deletedBy">User ID of the person who deleted</param>
    void SoftDelete(T entity, Guid? deletedBy = null);

    /// <summary>
    /// Hard delete - permanently removes the entity from the database.
    /// Only admin users should be allowed to call this.
    /// </summary>
    /// <param name="entity">Entity to permanently delete</param>
    void HardDelete(T entity);

    /// <summary>
    /// Hard delete multiple entities permanently.
    /// Only admin users should be allowed to call this.
    /// </summary>
    void HardDeleteRange(IEnumerable<T> entities);

    // Legacy methods - kept for backward compatibility
    [Obsolete("Use SoftDelete or HardDelete instead")]
    void Remove(T entity);

    [Obsolete("Use HardDeleteRange instead")]
    void RemoveRange(IEnumerable<T> entities);
}

