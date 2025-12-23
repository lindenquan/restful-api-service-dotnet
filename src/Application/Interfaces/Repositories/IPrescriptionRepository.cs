using Entities;

namespace Application.Interfaces.Repositories;

/// <summary>
/// Prescription-specific repository interface.
/// </summary>
public interface IPrescriptionRepository : IRepository<Prescription>
{
    Task<IEnumerable<Prescription>> GetByPatientIdAsync(int patientId, CancellationToken ct = default);
    Task<IEnumerable<Prescription>> GetActivePrescriptionsAsync(int patientId, CancellationToken ct = default);
    Task<IEnumerable<Prescription>> GetExpiredPrescriptionsAsync(int patientId, CancellationToken ct = default);
    Task<Prescription?> GetByIdWithPatientAsync(int id, CancellationToken ct = default);
}

