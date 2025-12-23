using Application.Interfaces.Repositories;
using Entities;
using MongoDB.Driver;

namespace Adapters.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPrescriptionOrderRepository.
/// </summary>
public class MongoPrescriptionOrderRepository : MongoRepository<PrescriptionOrder>, IPrescriptionOrderRepository
{
    private readonly IMongoCollection<Patient> _patientsCollection;
    private readonly IMongoCollection<Prescription> _prescriptionsCollection;

    public MongoPrescriptionOrderRepository(
        IMongoCollection<PrescriptionOrder> collection,
        IMongoCollection<Patient> patientsCollection,
        IMongoCollection<Prescription> prescriptionsCollection)
        : base(collection)
    {
        _patientsCollection = patientsCollection;
        _prescriptionsCollection = prescriptionsCollection;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByPatientIdAsync(int patientId, CancellationToken ct = default)
    {
        var orders = await _collection
            .Find(o => o.PatientId == patientId)
            .SortByDescending(o => o.OrderDate)
            .ToListAsync(ct);

        await LoadPrescriptionsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByPrescriptionIdAsync(int prescriptionId, CancellationToken ct = default)
    {
        var orders = await _collection
            .Find(o => o.PrescriptionId == prescriptionId)
            .SortByDescending(o => o.OrderDate)
            .ToListAsync(ct);

        await LoadPatientsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default)
    {
        var orders = await _collection
            .Find(o => o.Status == status)
            .SortByDescending(o => o.OrderDate)
            .ToListAsync(ct);

        await LoadDetailsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetPendingOrdersAsync(CancellationToken ct = default)
    {
        return await GetByStatusAsync(OrderStatus.Pending, ct);
    }

    public async Task<PrescriptionOrder?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
    {
        var order = await GetByIdAsync(id, ct);
        if (order == null)
            return null;

        // Load patient
        var patient = await _patientsCollection
            .Find(p => p.Id == order.PatientId)
            .FirstOrDefaultAsync(ct);
        order.Patient = patient;

        // Load prescription
        var prescription = await _prescriptionsCollection
            .Find(p => p.Id == order.PrescriptionId)
            .FirstOrDefaultAsync(ct);
        order.Prescription = prescription;

        return order;
    }

    /// <summary>
    /// Load prescriptions for a list of orders.
    /// </summary>
    private async Task LoadPrescriptionsAsync(List<PrescriptionOrder> orders, CancellationToken ct)
    {
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
    }

    /// <summary>
    /// Load patients for a list of orders.
    /// </summary>
    private async Task LoadPatientsAsync(List<PrescriptionOrder> orders, CancellationToken ct)
    {
        var patientIds = orders.Select(o => o.PatientId).Distinct().ToList();
        var patients = await _patientsCollection
            .Find(p => patientIds.Contains(p.Id))
            .ToListAsync(ct);

        var patientDict = patients.ToDictionary(p => p.Id);

        foreach (var order in orders)
        {
            if (patientDict.TryGetValue(order.PatientId, out var patient))
            {
                order.Patient = patient;
            }
        }
    }

    /// <summary>
    /// Load both patients and prescriptions for a list of orders.
    /// </summary>
    private async Task LoadDetailsAsync(List<PrescriptionOrder> orders, CancellationToken ct)
    {
        await LoadPatientsAsync(orders, ct);
        await LoadPrescriptionsAsync(orders, ct);
    }
}

