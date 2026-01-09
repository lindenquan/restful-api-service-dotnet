using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPatientRepository with transaction support.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoPatientRepository : MongoRepository<Patient, PatientDataModel>, IPatientRepository
{
    private readonly IMongoCollection<PrescriptionDataModel> _prescriptionsCollection;
    private readonly IMongoCollection<PrescriptionOrderDataModel> _ordersCollection;

    public MongoPatientRepository(
        IMongoCollection<PatientDataModel> collection,
        IMongoCollection<PrescriptionDataModel> prescriptionsCollection,
        IMongoCollection<PrescriptionOrderDataModel> ordersCollection,
        IResilientExecutor resilientExecutor,
        IMongoSessionProvider? sessionProvider = null)
        : base(collection, resilientExecutor, sessionProvider)
    {
        _prescriptionsCollection = prescriptionsCollection;
        _ordersCollection = ordersCollection;
    }

    protected override Patient ToDomain(PatientDataModel model) => PatientPersistenceMapper.ToDomain(model);
    protected override PatientDataModel ToDataModel(Patient entity) => PatientPersistenceMapper.ToDataModel(entity);

    public async Task<Patient?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var filter = Builders<PatientDataModel>.Filter.And(
            Builders<PatientDataModel>.Filter.Eq(u => u.Email, email),
            Builders<PatientDataModel>.Filter.Eq(u => u.Metadata.IsDeleted, false));

        var model = Session != null
            ? await _collection.Find(Session, filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(filter).FirstOrDefaultAsync(ct);

        return model == null ? null : ToDomain(model);
    }

    public async Task<Patient?> GetByIdWithPrescriptionsAsync(Guid id, CancellationToken ct = default)
    {
        var patient = await GetByIdAsync(id, ct);
        if (patient == null)
            return null;

        // MongoDB doesn't have native joins - manually load related data
        var prescriptionFilter = Builders<PrescriptionDataModel>.Filter.And(
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.PatientId, id),
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var prescriptionModels = Session != null
            ? await _prescriptionsCollection.Find(Session, prescriptionFilter).ToListAsync(ct)
            : await _prescriptionsCollection.Find(prescriptionFilter).ToListAsync(ct);

        patient.Prescriptions = prescriptionModels.Select(m => PrescriptionPersistenceMapper.ToDomain(m)).ToList();
        return patient;
    }

    public async Task<Patient?> GetByIdWithOrdersAsync(Guid id, CancellationToken ct = default)
    {
        var patient = await GetByIdAsync(id, ct);
        if (patient == null)
            return null;

        // Load orders with their prescriptions
        var orderFilter = Builders<PrescriptionOrderDataModel>.Filter.And(
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.PatientId, id),
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Metadata.IsDeleted, false));

        var orderModels = Session != null
            ? await _ordersCollection.Find(Session, orderFilter).ToListAsync(ct)
            : await _ordersCollection.Find(orderFilter).ToListAsync(ct);

        // Load prescriptions for each order
        var prescriptionIds = orderModels.Select(o => o.PrescriptionId).Distinct().ToList();
        var prescriptionFilter = Builders<PrescriptionDataModel>.Filter.And(
            Builders<PrescriptionDataModel>.Filter.In(p => p.Id, prescriptionIds),
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var prescriptionModels = Session != null
            ? await _prescriptionsCollection.Find(Session, prescriptionFilter).ToListAsync(ct)
            : await _prescriptionsCollection.Find(prescriptionFilter).ToListAsync(ct);

        var prescriptionDict = prescriptionModels.ToDictionary(p => p.Id, m => PrescriptionPersistenceMapper.ToDomain(m));

        var orders = orderModels.Select(m => PrescriptionOrderPersistenceMapper.ToDomain(m)).ToList();
        foreach (var order in orders)
        {
            if (prescriptionDict.TryGetValue(order.PrescriptionId, out var prescription))
            {
                order.Prescription = prescription;
            }
        }

        patient.Orders = orders;
        return patient;
    }
}

