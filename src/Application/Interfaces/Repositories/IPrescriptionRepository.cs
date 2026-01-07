using Domain;
using DTOs.Shared;

namespace Application.Interfaces.Repositories;

/// <summary>
/// Prescription-specific repository interface.
/// </summary>
public interface IPrescriptionRepository : IRepository<Prescription>
{
    Task<IEnumerable<Prescription>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IEnumerable<Prescription>> GetActivePrescriptionsAsync(Guid patientId, CancellationToken ct = default);
    Task<IEnumerable<Prescription>> GetExpiredPrescriptionsAsync(Guid patientId, CancellationToken ct = default);
    Task<Prescription?> GetByIdWithPatientAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Prescription>> GetAllWithPatientsAsync(CancellationToken ct = default);

    // Pagination methods with Patient navigation and OData filtering/sorting
    Task<PagedData<Prescription>> GetPagedWithPatientsAsync(
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default);
}

