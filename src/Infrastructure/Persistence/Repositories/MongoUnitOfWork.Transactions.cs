using Application.Interfaces.Repositories;
using Infrastructure.Persistence.Configuration;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Partial class containing transaction management operations.
/// </summary>
public sealed partial class MongoUnitOfWork
{
    /// <summary>
    /// MongoDB operations are auto-committed. This is a no-op unless in a transaction.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // MongoDB auto-commits each operation outside of transactions.
        // Within a transaction, changes are held until CommitTransactionAsync.
        // Return 1 to indicate success (matching EF Core behavior).
        return Task.FromResult(1);
    }

    /// <summary>
    /// Begin a MongoDB transaction with the configured isolation level.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a transaction is already active.</exception>
    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_session != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        var client = _database.Client;

        // Create session with configured options (including causal consistency)
        var sessionOptions = TransactionOptionsBuilder.BuildSessionOptions(_transactionSettings);
        _session = await client.StartSessionAsync(sessionOptions, ct);

        // Start transaction with configured isolation level (ReadConcern/WriteConcern)
        var transactionOptions = TransactionOptionsBuilder.Build(_transactionSettings);
        _session.StartTransaction(transactionOptions);

        // Clear cached repositories so they pick up the new session
        ClearRepositoryCache();
    }

    /// <summary>
    /// Begin a MongoDB transaction with a specific isolation level (overrides config).
    /// </summary>
    /// <param name="isolationLevel">The isolation level to use for this transaction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if a transaction is already active.</exception>
    public async Task BeginTransactionAsync(TransactionIsolationLevel isolationLevel, CancellationToken ct = default)
    {
        if (_session != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        var client = _database.Client;

        // Create temporary settings with the override isolation level
        var settings = new TransactionSettings
        {
            IsolationLevel = isolationLevel,
            MaxCommitTimeSeconds = _transactionSettings.MaxCommitTimeSeconds,
            RetryWrites = _transactionSettings.RetryWrites
        };

        var sessionOptions = TransactionOptionsBuilder.BuildSessionOptions(settings);
        _session = await client.StartSessionAsync(sessionOptions, ct);

        var transactionOptions = TransactionOptionsBuilder.Build(settings);
        _session.StartTransaction(transactionOptions);

        ClearRepositoryCache();
    }

    /// <summary>
    /// Commit the current MongoDB transaction.
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_session != null)
        {
            await _session.CommitTransactionAsync(ct);
            _session.Dispose();
            _session = null;
            ClearRepositoryCache();
        }
    }

    /// <summary>
    /// Rollback the current MongoDB transaction.
    /// All changes made since BeginTransactionAsync will be discarded.
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_session != null)
        {
            await _session.AbortTransactionAsync(ct);
            _session.Dispose();
            _session = null;
            ClearRepositoryCache();
        }
    }

    #region ExecuteInTransactionAsync Helpers

    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        await BeginTransactionAsync(ct);
        try
        {
            var result = await operation();
            await CommitTransactionAsync(ct);
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(
        TransactionIsolationLevel isolationLevel,
        Func<Task<T>> operation,
        CancellationToken ct = default)
    {
        await BeginTransactionAsync(isolationLevel, ct);
        try
        {
            var result = await operation();
            await CommitTransactionAsync(ct);
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
    {
        await BeginTransactionAsync(ct);
        try
        {
            await operation();
            await CommitTransactionAsync(ct);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        TransactionIsolationLevel isolationLevel,
        Func<Task> operation,
        CancellationToken ct = default)
    {
        await BeginTransactionAsync(isolationLevel, ct);
        try
        {
            await operation();
            await CommitTransactionAsync(ct);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
    }

    #endregion

    /// <summary>
    /// Clear cached repository instances so they're recreated with current session state.
    /// </summary>
    private void ClearRepositoryCache()
    {
        _patients = null;
        _prescriptions = null;
        _prescriptionOrders = null;
        _users = null;
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}

