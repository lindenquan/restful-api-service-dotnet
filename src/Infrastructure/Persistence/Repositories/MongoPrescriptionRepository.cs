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
        var filter = Builders<PrescriptionDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false);

        var query = _collection.Find(filter);
        query = ApplyPrescriptionOrdering(query, orderBy, descending);

        var models = await query
            .Skip(skip)
            .Limit(top)
            .ToListAsync(ct);

        long totalCount = 0;
        if (includeCount)
        {
            totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        }

        var prescriptions = models.Select(ToDomain).ToList();
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
    /// Apply ordering specific to prescriptions.
    /// </summary>
    private static IFindFluent<PrescriptionDataModel, PrescriptionDataModel> ApplyPrescriptionOrdering(
        IFindFluent<PrescriptionDataModel, PrescriptionDataModel> query,
        string? orderBy,
        bool descending)
    {
        // Default ordering by CreatedAt descending (newest first)
        if (string.IsNullOrEmpty(orderBy))
        {
            return query.SortByDescending(p => p.Metadata.CreatedAt);
        }

        var normalizedOrderBy = orderBy.ToLowerInvariant();

        return normalizedOrderBy switch
        {
            "expirydate" => descending
                ? query.SortByDescending(p => p.ExpiryDate)
                : query.SortBy(p => p.ExpiryDate),
            "prescribeddate" => descending
                ? query.SortByDescending(p => p.PrescribedDate)
                : query.SortBy(p => p.PrescribedDate),
            "medicationname" => descending
                ? query.SortByDescending(p => p.MedicationName)
                : query.SortBy(p => p.MedicationName),
            "createdat" => descending
                ? query.SortByDescending(p => p.Metadata.CreatedAt)
                : query.SortBy(p => p.Metadata.CreatedAt),
            "id" => descending
                ? query.SortByDescending(p => p.Id)
                : query.SortBy(p => p.Id),
            _ => query.SortByDescending(p => p.Metadata.CreatedAt) // Fallback to default
        };
    }
}

