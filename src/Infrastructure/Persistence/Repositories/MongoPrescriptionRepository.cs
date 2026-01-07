using Application.Interfaces.Repositories;
using Domain;
using DTOs.Shared;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPrescriptionRepository.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoPrescriptionRepository : MongoRepository<Prescription, PrescriptionDataModel>, IPrescriptionRepository
{
    private readonly IMongoCollection<PatientDataModel> _patientsCollection;

    public MongoPrescriptionRepository(
        IMongoCollection<PrescriptionDataModel> collection,
        IMongoCollection<PatientDataModel> patientsCollection,
        IResilientExecutor resilientExecutor)
        : base(collection, resilientExecutor)
    {
        _patientsCollection = patientsCollection;
    }

    protected override Prescription ToDomain(PrescriptionDataModel model) => PrescriptionPersistenceMapper.ToDomain(model);
    protected override PrescriptionDataModel ToDataModel(Prescription entity) => PrescriptionPersistenceMapper.ToDataModel(entity);

    public async Task<IEnumerable<Prescription>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
    {
        var models = await _collection
            .Find(p => p.PatientId == patientId && !p.Metadata.IsDeleted)
            .ToListAsync(ct);
        return models.Select(ToDomain);
    }

    public async Task<IEnumerable<Prescription>> GetActivePrescriptionsAsync(Guid patientId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var models = await _collection
            .Find(p => p.PatientId == patientId && p.ExpiryDate > now && p.RefillsRemaining > 0 && !p.Metadata.IsDeleted)
            .ToListAsync(ct);
        return models.Select(ToDomain);
    }

    public async Task<IEnumerable<Prescription>> GetExpiredPrescriptionsAsync(Guid patientId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var models = await _collection
            .Find(p => p.PatientId == patientId && p.ExpiryDate <= now && !p.Metadata.IsDeleted)
            .ToListAsync(ct);
        return models.Select(ToDomain);
    }

    public async Task<Prescription?> GetByIdWithPatientAsync(Guid id, CancellationToken ct = default)
    {
        var prescription = await GetByIdAsync(id, ct);
        if (prescription == null)
            return null;

        // Load the patient
        var patientModel = await _patientsCollection
            .Find(p => p.Id == prescription.PatientId && !p.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (patientModel != null)
        {
            prescription.Patient = PatientPersistenceMapper.ToDomain(patientModel);
        }
        return prescription;
    }

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

        var query = _collection.Find(filter);
        query = ApplyOrderingForPrescriptions(query, orderBy, descending);

        var prescriptionModels = await query
            .Skip(skip)
            .Limit(top)
            .ToListAsync(ct);

        long totalCount = 0;
        if (includeCount)
        {
            totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
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

        var patientModels = await _patientsCollection
            .Find(p => patientIds.Contains(p.Id) && !p.Metadata.IsDeleted)
            .ToListAsync(ct);

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

