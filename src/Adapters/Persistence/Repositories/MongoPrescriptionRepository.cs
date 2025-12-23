using Application.Interfaces.Repositories;
using Entities;
using MongoDB.Driver;

namespace Adapters.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPrescriptionRepository.
/// </summary>
public class MongoPrescriptionRepository : MongoRepository<Prescription>, IPrescriptionRepository
{
    private readonly IMongoCollection<Patient> _patientsCollection;

    public MongoPrescriptionRepository(
        IMongoCollection<Prescription> collection,
        IMongoCollection<Patient> patientsCollection)
        : base(collection)
    {
        _patientsCollection = patientsCollection;
    }

    public async Task<IEnumerable<Prescription>> GetByPatientIdAsync(int patientId, CancellationToken ct = default)
    {
        return await _collection
            .Find(p => p.PatientId == patientId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Prescription>> GetActivePrescriptionsAsync(int patientId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _collection
            .Find(p => p.PatientId == patientId && p.ExpiryDate > now && p.RefillsRemaining > 0)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Prescription>> GetExpiredPrescriptionsAsync(int patientId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _collection
            .Find(p => p.PatientId == patientId && p.ExpiryDate <= now)
            .ToListAsync(ct);
    }

    public async Task<Prescription?> GetByIdWithPatientAsync(int id, CancellationToken ct = default)
    {
        var prescription = await GetByIdAsync(id, ct);
        if (prescription == null)
            return null;

        // Load the patient
        var patient = await _patientsCollection
            .Find(p => p.Id == prescription.PatientId)
            .FirstOrDefaultAsync(ct);

        prescription.Patient = patient;
        return prescription;
    }
}

