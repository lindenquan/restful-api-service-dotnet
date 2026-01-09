namespace Application.Interfaces.Repositories;

/// <summary>
/// Transaction isolation levels for database operations.
/// </summary>
public enum TransactionIsolationLevel
{
    /// <summary>
    /// Default snapshot isolation. Provides consistent view of data at transaction start.
    /// Suitable for most applications requiring consistent reads within a transaction.
    /// </summary>
    Snapshot,

    /// <summary>
    /// Serializable isolation with linearizable reads. Strongest consistency guarantee.
    /// Suitable for banking, financial transactions, and systems requiring full ACID.
    /// Note: Higher latency due to waiting for majority acknowledgment.
    /// </summary>
    Serializable,

    /// <summary>
    /// Majority isolation. Reads data acknowledged by majority of replica set.
    /// Good balance between consistency and performance.
    /// </summary>
    Majority,

    /// <summary>
    /// Local isolation. Reads local data without waiting for replication.
    /// Highest performance, lowest consistency. Not recommended for transactions.
    /// </summary>
    Local
}

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
    /// Returns true if a transaction is currently active.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Save all changes made in this unit of work to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begin a new database transaction with the default isolation level.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Begin a new database transaction with a specific isolation level.
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <param name="ct">Cancellation token.</param>
    Task BeginTransactionAsync(TransactionIsolationLevel isolationLevel, CancellationToken ct = default);

    /// <summary>
    /// Commit the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Rollback the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Execute an operation within a transaction, automatically committing on success or rolling back on failure.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute within the transaction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);

    /// <summary>
    /// Execute an operation within a transaction with a specific isolation level.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <param name="operation">The async operation to execute within the transaction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteInTransactionAsync<T>(TransactionIsolationLevel isolationLevel, Func<Task<T>> operation, CancellationToken ct = default);

    /// <summary>
    /// Execute an operation within a transaction (no return value).
    /// </summary>
    /// <param name="operation">The async operation to execute within the transaction.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);

    /// <summary>
    /// Execute an operation within a transaction with a specific isolation level (no return value).
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <param name="operation">The async operation to execute within the transaction.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExecuteInTransactionAsync(TransactionIsolationLevel isolationLevel, Func<Task> operation, CancellationToken ct = default);
}

