using DTOs.V1;

namespace Contracts.V1;

/// <summary>
/// Pure API contract for Orders V1.
/// No ASP.NET Core dependencies - can be used by any client.
/// </summary>
public interface IOrdersApi
{
    /// <summary>
    /// Get all orders.
    /// </summary>
    Task<IEnumerable<OrderDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Get order by ID.
    /// Returns null if not found.
    /// </summary>
    Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Get orders by patient ID.
    /// </summary>
    Task<IEnumerable<OrderDto>> GetByPatientAsync(int patientId, CancellationToken ct = default);

    /// <summary>
    /// Create a new order.
    /// </summary>
    Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update order status.
    /// Returns null if not found.
    /// </summary>
    Task<OrderDto?> UpdateAsync(int id, UpdateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete an order (soft delete by default).
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="permanent">If true, performs hard delete (admin only)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(int id, bool permanent = false, CancellationToken ct = default);
}

