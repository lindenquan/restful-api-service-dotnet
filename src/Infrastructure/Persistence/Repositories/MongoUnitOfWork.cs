using Infrastructure.Persistence.Configuration;
using Application.Interfaces.Repositories;
using Domain;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of Unit of Work.
/// Note: MongoDB doesn't support traditional ACID transactions across collections
/// without replica sets. This implementation provides a compatible interface.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoUnitOfWork : IUnitOfWork
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<Patient> _patientsCollection;
    private readonly IMongoCollection<Prescription> _prescriptionsCollection;
    private readonly IMongoCollection<PrescriptionOrder> _ordersCollection;
    private readonly IMongoCollection<User> _usersCollection;

    private IPatientRepository? _patients;
    private IPrescriptionRepository? _prescriptions;
    private IPrescriptionOrderRepository? _prescriptionOrders;
    private IUserRepository? _users;

    private IClientSessionHandle? _session;

    public MongoUnitOfWork(IMongoClient client, MongoDbSettings settings)
    {
        _database = client.GetDatabase(settings.DatabaseName);

        _patientsCollection = _database.GetCollection<Patient>(settings.PatientsCollection);
        _prescriptionsCollection = _database.GetCollection<Prescription>(settings.PrescriptionsCollection);
        _ordersCollection = _database.GetCollection<PrescriptionOrder>(settings.OrdersCollection);
        _usersCollection = _database.GetCollection<User>(settings.UsersCollection);
    }

    public IPatientRepository Patients =>
        _patients ??= new MongoPatientRepository(_patientsCollection, _prescriptionsCollection, _ordersCollection);

    public IPrescriptionRepository Prescriptions =>
        _prescriptions ??= new MongoPrescriptionRepository(_prescriptionsCollection, _patientsCollection);

    public IPrescriptionOrderRepository PrescriptionOrders =>
        _prescriptionOrders ??= new MongoPrescriptionOrderRepository(
            _ordersCollection, _patientsCollection, _prescriptionsCollection);

    public IUserRepository Users =>
        _users ??= new MongoUserRepository(_usersCollection);

    /// <summary>
    /// MongoDB operations are auto-committed. This is a no-op unless in a transaction.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // MongoDB auto-commits each operation.
        // Return 1 to indicate success (matching EF Core behavior).
        return Task.FromResult(1);
    }

    /// <summary>
    /// Begin a MongoDB transaction.
    /// Note: Requires MongoDB replica set or sharded cluster.
    /// </summary>
    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        var client = _database.Client;
        _session = await client.StartSessionAsync(cancellationToken: ct);
        _session.StartTransaction();
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
        }
    }

    /// <summary>
    /// Rollback the current MongoDB transaction.
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_session != null)
        {
            await _session.AbortTransactionAsync(ct);
            _session.Dispose();
            _session = null;
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}

