using Contracts.V1;
using DTOs.V1;
using Refit;

namespace Adapters.ApiClient.V1;

/// <summary>
/// Refit-based HTTP client for Orders V1 API.
/// Implements IOrdersApi via HTTP calls.
/// </summary>
public interface IOrdersApiClient : IOrdersApi
{
    /// <summary>
    /// Get all orders.
    /// </summary>
    [Get("/api/v1/orders")]
    new Task<IEnumerable<OrderDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Get order by ID.
    /// Returns null if not found (404).
    /// </summary>
    [Get("/api/v1/orders/{id}")]
    new Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Get orders by patient ID.
    /// </summary>
    [Get("/api/v1/orders/patient/{patientId}")]
    new Task<IEnumerable<OrderDto>> GetByPatientAsync(int patientId, CancellationToken ct = default);

    /// <summary>
    /// Create a new order.
    /// </summary>
    [Post("/api/v1/orders")]
    new Task<OrderDto> CreateAsync([Body] CreateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update order status.
    /// Returns null if not found (404).
    /// </summary>
    [Put("/api/v1/orders/{id}")]
    new Task<OrderDto?> UpdateAsync(int id, [Body] UpdateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete an order (soft delete by default).
    /// </summary>
    [Delete("/api/v1/orders/{id}")]
    new Task<bool> DeleteAsync(int id, [Query] bool permanent = false, CancellationToken ct = default);
}

