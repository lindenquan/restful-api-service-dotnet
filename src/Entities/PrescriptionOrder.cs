namespace Entities;

/// <summary>
/// PrescriptionOrder entity representing an order to fulfill a prescription.
/// </summary>
public class PrescriptionOrder : BaseEntity
{
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }
    public DateTime? FulfilledDate { get; set; }
    public DateTime? PickupDate { get; set; }

    // Foreign keys
    public int PatientId { get; set; }
    public int PrescriptionId { get; set; }

    // Navigation properties
    public Patient? Patient { get; set; }
    public Prescription? Prescription { get; set; }
}

/// <summary>
/// Status of a prescription order.
/// </summary>
public enum OrderStatus
{
    Pending = 0,        // Order placed, waiting to be processed
    Processing = 1,     // Pharmacy is preparing the order
    Ready = 2,          // Ready for pickup
    Completed = 3,      // Order picked up / delivered
    Cancelled = 4       // Order was cancelled
}

