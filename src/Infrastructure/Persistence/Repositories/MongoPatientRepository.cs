using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPatientRepository.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoPatientRepository : MongoRepository<Patient>, IPatientRepository
{
    private readonly IMongoCollection<Prescription> _prescriptionsCollection;
    private readonly IMongoCollection<PrescriptionOrder> _ordersCollection;

    public MongoPatientRepository(
        IMongoCollection<Patient> collection,
        IMongoCollection<Prescription> prescriptionsCollection,
        IMongoCollection<PrescriptionOrder> ordersCollection,
        IResilientExecutor resilientExecutor)
        : base(collection, resilientExecutor)
    {
        _prescriptionsCollection = prescriptionsCollection;
        _ordersCollection = ordersCollection;
    }

    public async Task<Patient?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _collection.Find(u => u.Email == email).FirstOrDefaultAsync(ct);
    }

    public async Task<Patient?> GetByIdWithPrescriptionsAsync(int id, CancellationToken ct = default)
    {
        var patient = await GetByIdAsync(id, ct);
        if (patient == null)
            return null;

        // MongoDB doesn't have native joins - manually load related data
        var prescriptions = await _prescriptionsCollection
            .Find(p => p.PatientId == id)
            .ToListAsync(ct);

        patient.Prescriptions = prescriptions;
        return patient;
    }

    public async Task<Patient?> GetByIdWithOrdersAsync(int id, CancellationToken ct = default)
    {
        var patient = await GetByIdAsync(id, ct);
        if (patient == null)
            return null;

        // Load orders with their prescriptions
        var orders = await _ordersCollection
            .Find(o => o.PatientId == id)
            .ToListAsync(ct);

        // Load prescriptions for each order
        var prescriptionIds = orders.Select(o => o.PrescriptionId).Distinct().ToList();
        var prescriptions = await _prescriptionsCollection
            .Find(p => prescriptionIds.Contains(p.Id))
            .ToListAsync(ct);

        var prescriptionDict = prescriptions.ToDictionary(p => p.Id);

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

