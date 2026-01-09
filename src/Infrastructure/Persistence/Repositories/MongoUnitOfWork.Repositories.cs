using Application.Interfaces.Repositories;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Partial class containing repository property accessors.
/// </summary>
public sealed partial class MongoUnitOfWork
{
    public IPatientRepository Patients =>
        _patients ??= new MongoPatientRepository(
            _patientsCollection, _prescriptionsCollection, _ordersCollection, _resilientExecutor, this);

    public IPrescriptionRepository Prescriptions =>
        _prescriptions ??= new MongoPrescriptionRepository(
            _prescriptionsCollection, _patientsCollection, _resilientExecutor, this);

    public IPrescriptionOrderRepository PrescriptionOrders =>
        _prescriptionOrders ??= new MongoPrescriptionOrderRepository(
            _ordersCollection, _patientsCollection, _prescriptionsCollection, _resilientExecutor, this);

    public IUserRepository Users =>
        _users ??= new MongoUserRepository(_usersCollection, _resilientExecutor, this);
}

