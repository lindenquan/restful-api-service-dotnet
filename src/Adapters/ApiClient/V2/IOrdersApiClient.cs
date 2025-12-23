using Contracts.V2;
using DTOs.V2;
using Refit;

namespace Adapters.ApiClient.V2;

/// <summary>
/// Refit-based HTTP client for Orders V2 API.
/// Implements IOrdersApi via HTTP calls.
/// V2 uses more descriptive naming: "PrescriptionOrder" instead of "Order".
/// </summary>
public interface IOrdersApiClient : IOrdersApi
{
    /// <summary>
    /// Get all prescription orders.
    /// </summary>
    [Get("/api/v2/orders")]
    new Task<IEnumerable<PrescriptionOrderDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Get prescription order by ID.
    /// Returns null if not found (404).
    /// </summary>
    [Get("/api/v2/orders/{id}")]
    new Task<PrescriptionOrderDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Get prescription orders by patient ID.
    /// </summary>
    [Get("/api/v2/orders/patient/{patientId}")]
    new Task<IEnumerable<PrescriptionOrderDto>> GetByPatientAsync(int patientId, CancellationToken ct = default);

    /// <summary>
    /// Get prescription orders by status.
    /// </summary>
    [Get("/api/v2/orders/status/{status}")]
    new Task<IEnumerable<PrescriptionOrderDto>> GetByStatusAsync(string status, CancellationToken ct = default);

    /// <summary>
    /// Create a new prescription order.
    /// V2 supports IsUrgent flag.
    /// </summary>
    [Post("/api/v2/orders")]
    new Task<PrescriptionOrderDto> CreateAsync([Body] CreatePrescriptionOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update prescription order status.
    /// V2 supports updating both status and notes.
    /// Returns null if not found (404).
    /// </summary>
    [Put("/api/v2/orders/{id}")]
    new Task<PrescriptionOrderDto?> UpdateAsync(int id, [Body] UpdatePrescriptionOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a prescription order (soft delete by default).
    /// </summary>
    [Delete("/api/v2/orders/{id}")]
    new Task<bool> DeleteAsync(int id, [Query] bool permanent = false, CancellationToken ct = default);
}

