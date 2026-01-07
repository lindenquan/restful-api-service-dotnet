using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPatientRepository.
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
        IResilientExecutor resilientExecutor)
        : base(collection, resilientExecutor)
    {
        _prescriptionsCollection = prescriptionsCollection;
        _ordersCollection = ordersCollection;
    }

    protected override Patient ToDomain(PatientDataModel model) => PatientPersistenceMapper.ToDomain(model);
    protected override PatientDataModel ToDataModel(Patient entity) => PatientPersistenceMapper.ToDataModel(entity);

    public async Task<Patient?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var model = await _collection.Find(u => u.Email == email && !u.Metadata.IsDeleted).FirstOrDefaultAsync(ct);
        return model == null ? null : ToDomain(model);
    }

    public async Task<Patient?> GetByIdWithPrescriptionsAsync(Guid id, CancellationToken ct = default)
    {
        var patient = await GetByIdAsync(id, ct);
        if (patient == null)
            return null;

        // MongoDB doesn't have native joins - manually load related data
        var prescriptionModels = await _prescriptionsCollection
            .Find(p => p.PatientId == id && !p.Metadata.IsDeleted)
            .ToListAsync(ct);

        patient.Prescriptions = prescriptionModels.Select(PrescriptionPersistenceMapper.ToDomain).ToList();
        return patient;
    }

    public async Task<Patient?> GetByIdWithOrdersAsync(Guid id, CancellationToken ct = default)
    {
        var patient = await GetByIdAsync(id, ct);
        if (patient == null)
            return null;

        // Load orders with their prescriptions
        var orderModels = await _ordersCollection
            .Find(o => o.PatientId == id && !o.Metadata.IsDeleted)
            .ToListAsync(ct);

        // Load prescriptions for each order
        var prescriptionIds = orderModels.Select(o => o.PrescriptionId).Distinct().ToList();
        var prescriptionModels = await _prescriptionsCollection
            .Find(p => prescriptionIds.Contains(p.Id) && !p.Metadata.IsDeleted)
            .ToListAsync(ct);

        var prescriptionDict = prescriptionModels.ToDictionary(p => p.Id, PrescriptionPersistenceMapper.ToDomain);

        var orders = orderModels.Select(PrescriptionOrderPersistenceMapper.ToDomain).ToList();
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

