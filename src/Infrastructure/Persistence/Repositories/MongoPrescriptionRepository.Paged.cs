using Domain;
using DTOs.Shared;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Partial class containing pagination and patient loading operations.
/// </summary>
public sealed partial class MongoPrescriptionRepository
{
    public async Task<IEnumerable<Prescription>> GetAllWithPatientsAsync(CancellationToken ct = default)
    {
        var prescriptions = (await GetAllAsync(ct)).ToList();
        await LoadPatientsAsync(prescriptions, ct);
        return prescriptions;
    }

    public async Task<PagedData<Prescription>> GetPagedWithPatientsAsync(
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default)
    {
        var filter = Builders<PrescriptionDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);

        var query = Session != null
            ? _collection.Find(Session, filter)
            : _collection.Find(filter);
        query = ApplyOrderingForPrescriptions(query, orderBy, descending);

        var prescriptionModels = await query
            .Skip(skip)
            .Limit(top)
            .ToListAsync(ct);

        long totalCount = 0;
        if (includeCount)
        {
            totalCount = Session != null
                ? await _collection.CountDocumentsAsync(Session, filter, cancellationToken: ct)
                : await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        }

        var prescriptions = prescriptionModels.Select(ToDomain).ToList();
        await LoadPatientsAsync(prescriptions, ct);

        return new PagedData<Prescription>(prescriptions, totalCount);
    }

    /// <summary>
    /// Load patients for a list of prescriptions.
    /// </summary>
    private async Task LoadPatientsAsync(List<Prescription> prescriptions, CancellationToken ct)
    {
        var patientIds = prescriptions.Select(p => p.PatientId).Distinct().ToList();

        var patientFilter = Builders<PatientDataModel>.Filter.And(
            Builders<PatientDataModel>.Filter.In(p => p.Id, patientIds),
            Builders<PatientDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var patientModels = Session != null
            ? await _patientsCollection.Find(Session, patientFilter).ToListAsync(ct)
            : await _patientsCollection.Find(patientFilter).ToListAsync(ct);

        var patientMap = patientModels.ToDictionary(
            p => p.Id,
            p => PatientPersistenceMapper.ToDomain(p));

        foreach (var prescription in prescriptions)
        {
            if (patientMap.TryGetValue(prescription.PatientId, out var patient))
            {
                prescription.Patient = patient;
            }
        }
    }

    /// <summary>
    /// Apply ordering specific to Prescription fields.
    /// </summary>
    private static IFindFluent<PrescriptionDataModel, PrescriptionDataModel> ApplyOrderingForPrescriptions(
        IFindFluent<PrescriptionDataModel, PrescriptionDataModel> query,
        string? orderBy,
        bool descending)
    {
        if (string.IsNullOrEmpty(orderBy))
        {
            return query.SortByDescending(e => e.PrescribedDate);
        }

        var normalizedOrderBy = orderBy.ToLowerInvariant();

        return normalizedOrderBy switch
        {
            "prescribeddate" => descending
                ? query.SortByDescending(e => e.PrescribedDate)
                : query.SortBy(e => e.PrescribedDate),
            "expirydate" => descending
                ? query.SortByDescending(e => e.ExpiryDate)
                : query.SortBy(e => e.ExpiryDate),
            "medicationname" => descending
                ? query.SortByDescending(e => e.MedicationName)
                : query.SortBy(e => e.MedicationName),
            "prescribername" => descending
                ? query.SortByDescending(e => e.PrescriberName)
                : query.SortBy(e => e.PrescriberName),
            "quantity" => descending
                ? query.SortByDescending(e => e.Quantity)
                : query.SortBy(e => e.Quantity),
            "refillsremaining" => descending
                ? query.SortByDescending(e => e.RefillsRemaining)
                : query.SortBy(e => e.RefillsRemaining),
            "createdat" => descending
                ? query.SortByDescending(e => e.Metadata.CreatedAt)
                : query.SortBy(e => e.Metadata.CreatedAt),
            _ => query.SortByDescending(e => e.PrescribedDate)
        };
    }
}

