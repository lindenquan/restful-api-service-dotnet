using Domain;

namespace Application.Interfaces.Repositories;

/// <summary>
/// PrescriptionOrder-specific repository interface.
/// </summary>
public interface IPrescriptionOrderRepository : IRepository<PrescriptionOrder>
{
    Task<IEnumerable<PrescriptionOrder>> GetByPatientIdAsync(int patientId, CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetByPrescriptionIdAsync(int prescriptionId, CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default);
    Task<IEnumerable<PrescriptionOrder>> GetPendingOrdersAsync(CancellationToken ct = default);
    Task<PrescriptionOrder?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
}

