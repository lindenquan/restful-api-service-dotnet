using Application.Interfaces.Repositories;

namespace Infrastructure.Persistence.Configuration;

/// <summary>
/// Configuration settings for MongoDB transactions.
/// </summary>
public class TransactionSettings
{
    /// <summary>
    /// Default transaction isolation level.
    /// Default: Snapshot (MongoDB's default for multi-document transactions)
    /// </summary>
    public TransactionIsolationLevel IsolationLevel { get; set; } = TransactionIsolationLevel.Snapshot;

    /// <summary>
    /// Maximum time (seconds) a transaction can run before being automatically aborted.
    /// Default: 60 seconds. Set to 0 for no limit (not recommended).
    /// For long-running financial transactions, consider increasing this value.
    /// </summary>
    public int MaxCommitTimeSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to retry failed commits automatically.
    /// Default: true. MongoDB driver will retry once on transient errors.
    /// </summary>
    public bool RetryWrites { get; set; } = true;

    /// <summary>
    /// Gets whether causal consistency should be enabled.
    /// Automatically enabled for Serializable isolation level.
    /// </summary>
    internal bool CausalConsistency => IsolationLevel == TransactionIsolationLevel.Serializable;
}

