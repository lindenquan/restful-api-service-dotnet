using Domain;
using DTOs.Shared;

namespace Application.Interfaces.Repositories;

/// <summary>
/// PrescriptionOrder-specific repository interface.
/// </summary>
public interface IPrescriptionOrderRepository : IRepository<PrescriptionOrder>
{
    Task<IEnumerable<PrescriptionOrder>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetByPrescriptionIdAsync(Guid prescriptionId, CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetPendingOrdersAsync(CancellationToken ct = default);
    Task<PrescriptionOrder?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetAllWithDetailsAsync(CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetByPatientIdWithDetailsAsync(Guid patientId, CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetByStatusWithDetailsAsync(OrderStatus status, CancellationToken ct = default);

    // Pagination methods with details (includes Patient and Prescription navigation) and OData filtering/sorting
    Task<PagedData<PrescriptionOrder>> GetPagedWithDetailsAsync(
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default);

    Task<PagedData<PrescriptionOrder>> GetPagedByPatientWithDetailsAsync(
        Guid patientId,
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default);
}

