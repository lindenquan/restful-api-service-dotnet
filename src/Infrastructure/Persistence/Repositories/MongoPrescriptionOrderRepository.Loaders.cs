using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Partial class containing related entity loading methods.
/// These methods batch-load patients and prescriptions for order lists.
/// </summary>
public sealed partial class MongoPrescriptionOrderRepository
{
    /// <summary>
    /// Load prescriptions for a list of orders.
    /// </summary>
    private async Task LoadPrescriptionsAsync(List<PrescriptionOrder> orders, CancellationToken ct)
    {
        if (orders.Count == 0)
            return;

        var prescriptionIds = orders.Select(o => o.PrescriptionId).Distinct().ToList();

        var prescriptionFilter = Builders<PrescriptionDataModel>.Filter.And(
            Builders<PrescriptionDataModel>.Filter.In(p => p.Id, prescriptionIds),
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var prescriptionModels = Session != null
            ? await _prescriptionsCollection.Find(Session, prescriptionFilter).ToListAsync(ct)
            : await _prescriptionsCollection.Find(prescriptionFilter).ToListAsync(ct);

        var prescriptionDict = prescriptionModels.ToDictionary(
            p => p.Id,
            p => PrescriptionPersistenceMapper.ToDomain(p));

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
        if (orders.Count == 0)
            return;

        var patientIds = orders.Select(o => o.PatientId).Distinct().ToList();

        var patientFilter = Builders<PatientDataModel>.Filter.And(
            Builders<PatientDataModel>.Filter.In(p => p.Id, patientIds),
            Builders<PatientDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var patientModels = Session != null
            ? await _patientsCollection.Find(Session, patientFilter).ToListAsync(ct)
            : await _patientsCollection.Find(patientFilter).ToListAsync(ct);

        var patientDict = patientModels.ToDictionary(
            p => p.Id,
            p => PatientPersistenceMapper.ToDomain(p));

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

