using Application.Interfaces.Repositories;
using Infrastructure.Persistence.Configuration;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of Unit of Work with transaction support.
/// <para>
/// <strong>Transaction Requirements:</strong>
/// MongoDB transactions require a replica set or sharded cluster.
/// Single-node deployments can use replica set mode for development.
/// </para>
/// <para>
/// <strong>Usage:</strong>
/// <code>
/// await unitOfWork.BeginTransactionAsync(ct);
/// try {
///     await unitOfWork.Patients.AddAsync(patient, ct);
///     await unitOfWork.Prescriptions.AddAsync(prescription, ct);
///     await unitOfWork.CommitTransactionAsync(ct);
/// } catch {
///     await unitOfWork.RollbackTransactionAsync(ct);
///     throw;
/// }
/// </code>
/// </para>
/// <para>
/// This class is split into partial classes for maintainability:
/// <list type="bullet">
///   <item><description>MongoUnitOfWork.cs - Constructor and fields</description></item>
///   <item><description>MongoUnitOfWork.Repositories.cs - Repository properties</description></item>
///   <item><description>MongoUnitOfWork.Transactions.cs - Transaction management</description></item>
/// </list>
/// </para>
/// </summary>
public sealed partial class MongoUnitOfWork : IUnitOfWork, IMongoSessionProvider
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<PatientDataModel> _patientsCollection;
    private readonly IMongoCollection<PrescriptionDataModel> _prescriptionsCollection;
    private readonly IMongoCollection<PrescriptionOrderDataModel> _ordersCollection;
    private readonly IMongoCollection<UserDataModel> _usersCollection;
    private readonly IResilientExecutor _resilientExecutor;
    private readonly TransactionSettings _transactionSettings;

    private IPatientRepository? _patients;
    private IPrescriptionRepository? _prescriptions;
    private IPrescriptionOrderRepository? _prescriptionOrders;
    private IUserRepository? _users;

    private IClientSessionHandle? _session;

    public MongoUnitOfWork(IMongoClient client, MongoDbSettings settings, IResilientExecutor resilientExecutor)
    {
        _database = client.GetDatabase(settings.DatabaseName);
        _resilientExecutor = resilientExecutor;
        _transactionSettings = settings.Transaction;

        _patientsCollection = _database.GetCollection<PatientDataModel>(settings.PatientsCollection);
        _prescriptionsCollection = _database.GetCollection<PrescriptionDataModel>(settings.PrescriptionsCollection);
        _ordersCollection = _database.GetCollection<PrescriptionOrderDataModel>(settings.OrdersCollection);
        _usersCollection = _database.GetCollection<UserDataModel>(settings.UsersCollection);
    }

    /// <summary>
    /// Gets the current transaction isolation level.
    /// </summary>
    public TransactionIsolationLevel IsolationLevel => _transactionSettings.IsolationLevel;

    #region IMongoSessionProvider

    /// <inheritdoc />
    public IClientSessionHandle? CurrentSession => _session;

    /// <inheritdoc />
    public bool HasActiveTransaction => _session?.IsInTransaction ?? false;

    #endregion
}

