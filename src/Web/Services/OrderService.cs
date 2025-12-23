using Adapters.ApiClient.V1;
using DTOs.V1;

namespace Web.Services;

/// <summary>
/// Service for managing prescription orders with business logic.
/// This is testable business logic separate from UI components.
/// </summary>
public class OrderService
{
    private readonly IOrdersApiClient _ordersApi;

    public OrderService(IOrdersApiClient ordersApi)
    {
        _ordersApi = ordersApi;
    }

    /// <summary>
    /// Get all orders for display.
    /// </summary>
    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync(CancellationToken ct = default)
    {
        return await _ordersApi.GetAllAsync(ct);
    }

    /// <summary>
    /// Get orders by patient ID.
    /// </summary>
    public async Task<IEnumerable<OrderDto>> GetOrdersByPatientAsync(int patientId, CancellationToken ct = default)
    {
        return await _ordersApi.GetByPatientAsync(patientId, ct);
    }

    /// <summary>
    /// Get order by ID.
    /// </summary>
    public async Task<OrderDto?> GetOrderByIdAsync(int orderId, CancellationToken ct = default)
    {
        return await _ordersApi.GetByIdAsync(orderId, ct);
    }

    /// <summary>
    /// Create a new prescription order.
    /// </summary>
    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        return await _ordersApi.CreateAsync(request, ct);
    }

    /// <summary>
    /// Validate order creation request.
    /// Returns error message if invalid, null if valid.
    /// </summary>
    public string? ValidateCreateOrderRequest(CreateOrderRequest request)
    {
        if (request.PatientId <= 0)
            return "Patient ID must be greater than 0";

        if (request.PrescriptionId <= 0)
            return "Prescription ID must be greater than 0";

        return null;
    }

    /// <summary>
    /// Get status badge color for UI.
    /// </summary>
    public string GetStatusBadgeColor(string status)
    {
        return status.ToLower() switch
        {
            "pending" => "warning",
            "processing" => "info",
            "ready" => "primary",
            "completed" => "success",
            "cancelled" => "danger",
            _ => "secondary"
        };
    }

    /// <summary>
    /// Check if order can be cancelled (only Pending or Processing orders).
    /// </summary>
    public bool CanCancelOrder(string status)
    {
        return status.ToLower() is "pending" or "processing";
    }
}

