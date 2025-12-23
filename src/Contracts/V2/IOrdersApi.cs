using DTOs.V2;

namespace Contracts.V2;

/// <summary>
/// Pure API contract for Orders V2.
/// No ASP.NET Core dependencies - can be used by any client.
/// V2 uses more descriptive naming: "PrescriptionOrder" instead of "Order".
/// </summary>
public interface IOrdersApi
{
    /// <summary>
    /// Get all prescription orders.
    /// </summary>
    Task<IEnumerable<PrescriptionOrderDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Get prescription order by ID.
    /// Returns null if not found.
    /// </summary>
    Task<PrescriptionOrderDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Get prescription orders by patient ID.
    /// </summary>
    Task<IEnumerable<PrescriptionOrderDto>> GetByPatientAsync(int patientId, CancellationToken ct = default);

    /// <summary>
    /// Get prescription orders by status.
    /// </summary>
    Task<IEnumerable<PrescriptionOrderDto>> GetByStatusAsync(string status, CancellationToken ct = default);

    /// <summary>
    /// Create a new prescription order.
    /// V2 supports IsUrgent flag.
    /// </summary>
    Task<PrescriptionOrderDto> CreateAsync(CreatePrescriptionOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update prescription order status.
    /// V2 supports updating both status and notes.
    /// Returns null if not found.
    /// </summary>
    Task<PrescriptionOrderDto?> UpdateAsync(int id, UpdatePrescriptionOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a prescription order (soft delete by default).
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="permanent">If true, performs hard delete (admin only)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(int id, bool permanent = false, CancellationToken ct = default);
}

