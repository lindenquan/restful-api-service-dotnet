using Application.Interfaces.Repositories;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Configuration;

/// <summary>
/// Builds MongoDB TransactionOptions based on configured isolation level.
/// </summary>
public static class TransactionOptionsBuilder
{
    /// <summary>
    /// Create TransactionOptions from TransactionSettings.
    /// </summary>
    public static TransactionOptions Build(TransactionSettings settings)
    {
        var (readConcern, writeConcern) = GetConcerns(settings.IsolationLevel);

        return new TransactionOptions(
            readConcern: readConcern,
            writeConcern: writeConcern,
            maxCommitTime: settings.MaxCommitTimeSeconds > 0
                ? TimeSpan.FromSeconds(settings.MaxCommitTimeSeconds)
                : null
        );
    }

    /// <summary>
    /// Create ClientSessionOptions for the given settings.
    /// </summary>
    public static ClientSessionOptions BuildSessionOptions(TransactionSettings settings)
    {
        return new ClientSessionOptions
        {
            CausalConsistency = settings.CausalConsistency,
            DefaultTransactionOptions = Build(settings)
        };
    }

    /// <summary>
    /// Get ReadConcern and WriteConcern for the specified isolation level.
    /// </summary>
    private static (ReadConcern, WriteConcern) GetConcerns(TransactionIsolationLevel level)
    {
        return level switch
        {
            // Snapshot: Consistent view at transaction start
            // Best for most OLTP applications
            TransactionIsolationLevel.Snapshot => (
                ReadConcern.Snapshot,
                WriteConcern.WMajority
            ),

            // Serializable: Strongest isolation with linearizable reads
            // For banking, financial systems requiring full ACID
            // Waits for all replica set members to acknowledge
            TransactionIsolationLevel.Serializable => (
                ReadConcern.Linearizable,
                WriteConcern.WMajority.With(journal: true)
            ),

            // Majority: Reads data acknowledged by majority
            // Good balance of consistency and performance
            TransactionIsolationLevel.Majority => (
                ReadConcern.Majority,
                WriteConcern.WMajority
            ),

            // Local: Reads local data, single node acknowledgment
            // Highest performance, use only for non-critical data
            TransactionIsolationLevel.Local => (
                ReadConcern.Local,
                WriteConcern.W1
            ),

            _ => (ReadConcern.Snapshot, WriteConcern.WMajority)
        };
    }

    /// <summary>
    /// Get a human-readable description of the isolation level.
    /// </summary>
    public static string GetDescription(TransactionIsolationLevel level)
    {
        return level switch
        {
            TransactionIsolationLevel.Snapshot =>
                "Snapshot isolation - consistent view at transaction start",
            TransactionIsolationLevel.Serializable =>
                "Serializable isolation - full ACID with linearizable reads (banking/financial)",
            TransactionIsolationLevel.Majority =>
                "Majority isolation - reads majority-acknowledged data",
            TransactionIsolationLevel.Local =>
                "Local isolation - reads local data without replication wait",
            _ => "Unknown isolation level"
        };
    }
}

