using System.Linq.Expressions;

namespace Application.Interfaces.Repositories;

/// <summary>
/// Generic repository interface for CRUD operations.
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IRepository<T> where T : class
{
    // Query (excludes soft-deleted by default)
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    // Commands
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    void Update(T entity);

    /// <summary>
    /// Soft delete - marks the entity as deleted without removing from database.
    /// </summary>
    /// <param name="entity">Entity to soft delete</param>
    /// <param name="deletedBy">Username of the person who deleted</param>
    void SoftDelete(T entity, string? deletedBy = null);

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

