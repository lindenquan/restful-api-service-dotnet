namespace Application.Interfaces.Repositories;

/// <summary>
/// Unit of Work pattern - coordinates multiple repository operations in a single transaction.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IPatientRepository Patients { get; }
    IPrescriptionRepository Prescriptions { get; }
    IPrescriptionOrderRepository PrescriptionOrders { get; }
    IUserRepository Users { get; }

    /// <summary>
    /// Save all changes made in this unit of work to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begin a new database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Commit the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Rollback the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

