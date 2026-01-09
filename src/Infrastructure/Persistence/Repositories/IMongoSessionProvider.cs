using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Provides access to the current MongoDB session for transaction support.
/// Repositories use this to participate in transactions started by the Unit of Work.
/// </summary>
public interface IMongoSessionProvider
{
    /// <summary>
    /// Gets the current session if a transaction is active, null otherwise.
    /// </summary>
    IClientSessionHandle? CurrentSession { get; }

    /// <summary>
    /// Returns true if a transaction is currently active.
    /// </summary>
    bool HasActiveTransaction { get; }
}

