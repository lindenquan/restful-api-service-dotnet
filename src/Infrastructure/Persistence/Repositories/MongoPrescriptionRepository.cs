using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPrescriptionRepository with transaction support.
/// Sealed for performance optimization and design intent.
/// <para>
/// This class is split into partial classes for maintainability:
/// <list type="bullet">
///   <item><description>MongoPrescriptionRepository.cs - Core operations and constructors</description></item>
///   <item><description>MongoPrescriptionRepository.Paged.cs - Pagination and patient loading</description></item>
/// </list>
/// </para>
/// </summary>
public sealed partial class MongoPrescriptionRepository : MongoRepository<Prescription, PrescriptionDataModel>, IPrescriptionRepository
{
    private readonly IMongoCollection<PatientDataModel> _patientsCollection;

    public MongoPrescriptionRepository(
        IMongoCollection<PrescriptionDataModel> collection,
        IMongoCollection<PatientDataModel> patientsCollection,
        IResilientExecutor resilientExecutor,
        IMongoSessionProvider? sessionProvider = null)
        : base(collection, resilientExecutor, sessionProvider)
    {
        _patientsCollection = patientsCollection;
    }

    protected override Prescription ToDomain(PrescriptionDataModel model) => PrescriptionPersistenceMapper.ToDomain(model);
    protected override PrescriptionDataModel ToDataModel(Prescription entity) => PrescriptionPersistenceMapper.ToDataModel(entity);

    public async Task<IEnumerable<Prescription>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
    {
        var filter = Builders<PrescriptionDataModel>.Filter.And(
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.PatientId, patientId),
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var models = Session != null
            ? await _collection.Find(Session, filter).ToListAsync(ct)
            : await _collection.Find(filter).ToListAsync(ct);

        return models.Select(ToDomain);
    }

    public async Task<IEnumerable<Prescription>> GetActivePrescriptionsAsync(Guid patientId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<PrescriptionDataModel>.Filter.And(
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.PatientId, patientId),
            Builders<PrescriptionDataModel>.Filter.Gt(p => p.ExpiryDate, now),
            Builders<PrescriptionDataModel>.Filter.Gt(p => p.RefillsRemaining, 0),
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var models = Session != null
            ? await _collection.Find(Session, filter).ToListAsync(ct)
            : await _collection.Find(filter).ToListAsync(ct);

        return models.Select(ToDomain);
    }

    public async Task<IEnumerable<Prescription>> GetExpiredPrescriptionsAsync(Guid patientId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<PrescriptionDataModel>.Filter.And(
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.PatientId, patientId),
            Builders<PrescriptionDataModel>.Filter.Lte(p => p.ExpiryDate, now),
            Builders<PrescriptionDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var models = Session != null
            ? await _collection.Find(Session, filter).ToListAsync(ct)
            : await _collection.Find(filter).ToListAsync(ct);

        return models.Select(ToDomain);
    }

    public async Task<Prescription?> GetByIdWithPatientAsync(Guid id, CancellationToken ct = default)
    {
        var prescription = await GetByIdAsync(id, ct);
        if (prescription == null)
            return null;

        // Load the patient
        var patientFilter = Builders<PatientDataModel>.Filter.And(
            Builders<PatientDataModel>.Filter.Eq(p => p.Id, prescription.PatientId),
            Builders<PatientDataModel>.Filter.Eq(p => p.Metadata.IsDeleted, false));

        var patientModel = Session != null
            ? await _patientsCollection.Find(Session, patientFilter).FirstOrDefaultAsync(ct)
            : await _patientsCollection.Find(patientFilter).FirstOrDefaultAsync(ct);

        if (patientModel != null)
        {
            prescription.Patient = PatientPersistenceMapper.ToDomain(patientModel);
        }
        return prescription;
    }
}

