using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPrescriptionOrderRepository with transaction support.
/// <para>
/// This class is split into partial classes for maintainability:
/// <list type="bullet">
///   <item><description>MongoPrescriptionOrderRepository.cs - Core queries and constructor</description></item>
///   <item><description>MongoPrescriptionOrderRepository.Paged.cs - Paged query methods</description></item>
///   <item><description>MongoPrescriptionOrderRepository.Loaders.cs - Related entity loading</description></item>
/// </list>
/// </para>
/// </summary>
public sealed partial class MongoPrescriptionOrderRepository : MongoRepository<PrescriptionOrder, PrescriptionOrderDataModel>, IPrescriptionOrderRepository
{
    private readonly IMongoCollection<PatientDataModel> _patientsCollection;
    private readonly IMongoCollection<PrescriptionDataModel> _prescriptionsCollection;

    public MongoPrescriptionOrderRepository(
        IMongoCollection<PrescriptionOrderDataModel> collection,
        IMongoCollection<PatientDataModel> patientsCollection,
        IMongoCollection<PrescriptionDataModel> prescriptionsCollection,
        IResilientExecutor resilientExecutor,
        IMongoSessionProvider? sessionProvider = null)
        : base(collection, resilientExecutor, sessionProvider)
    {
        _patientsCollection = patientsCollection;
        _prescriptionsCollection = prescriptionsCollection;
    }

    protected override PrescriptionOrder ToDomain(PrescriptionOrderDataModel model) =>
        PrescriptionOrderPersistenceMapper.ToDomain(model);

    protected override PrescriptionOrderDataModel ToDataModel(PrescriptionOrder entity) =>
        PrescriptionOrderPersistenceMapper.ToDataModel(entity);

    public async Task<IEnumerable<PrescriptionOrder>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
    {
        var filter = Builders<PrescriptionOrderDataModel>.Filter.And(
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.PatientId, patientId),
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Metadata.IsDeleted, false));

        var orderModels = Session != null
            ? await _collection.Find(Session, filter).SortByDescending(o => o.OrderDate).ToListAsync(ct)
            : await _collection.Find(filter).SortByDescending(o => o.OrderDate).ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadPrescriptionsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByPrescriptionIdAsync(Guid prescriptionId, CancellationToken ct = default)
    {
        var filter = Builders<PrescriptionOrderDataModel>.Filter.And(
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.PrescriptionId, prescriptionId),
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Metadata.IsDeleted, false));

        var orderModels = Session != null
            ? await _collection.Find(Session, filter).SortByDescending(o => o.OrderDate).ToListAsync(ct)
            : await _collection.Find(filter).SortByDescending(o => o.OrderDate).ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadPatientsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default)
    {
        var dataStatus = (OrderStatusData)status;
        var filter = Builders<PrescriptionOrderDataModel>.Filter.And(
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Status, dataStatus),
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Metadata.IsDeleted, false));

        var orderModels = Session != null
            ? await _collection.Find(Session, filter).SortByDescending(o => o.OrderDate).ToListAsync(ct)
            : await _collection.Find(filter).SortByDescending(o => o.OrderDate).ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadDetailsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetPendingOrdersAsync(CancellationToken ct = default)
    {
        return await GetByStatusAsync(OrderStatus.Pending, ct);
    }

    public async Task<PrescriptionOrder?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        var order = await GetByIdAsync(id, ct);
        if (order == null)
            return null;

        // Load patient
        var patientFilter = Builders<PatientDataModel>.Filter.And(
            Builders<PatientDataModel>.Filter.Eq(p => p.Id, order.PatientId),
            Builders<PatientDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var patientModel = Session != null
            ? await _patientsCollection.Find(Session, patientFilter).FirstOrDefaultAsync(ct)
            : await _patientsCollection.Find(patientFilter).FirstOrDefaultAsync(ct);

        if (patientModel != null)
        {
            order.Patient = PatientPersistenceMapper.ToDomain(patientModel);
        }

        // Load prescription
        var prescriptionFilter = Builders<PrescriptionDataModel>.Filter.And(
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.Id, order.PrescriptionId),
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var prescriptionModel = Session != null
            ? await _prescriptionsCollection.Find(Session, prescriptionFilter).FirstOrDefaultAsync(ct)
            : await _prescriptionsCollection.Find(prescriptionFilter).FirstOrDefaultAsync(ct);

        if (prescriptionModel != null)
        {
            order.Prescription = PrescriptionPersistenceMapper.ToDomain(prescriptionModel);
        }

        return order;
    }
}

