namespace Infrastructure.Persistence.Models;

/// <summary>
/// Order status enum - duplicated here to avoid domain dependency.
/// </summary>
public enum OrderStatusData
{
    Pending = 0,
    Processing = 1,
    Ready = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>
/// MongoDB data model for PrescriptionOrder.
/// Contains domain data plus infrastructure metadata.
/// </summary>
public class PrescriptionOrderDataModel : BaseDataModel
{
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public OrderStatusData Status { get; set; } = OrderStatusData.Pending;
    public string? Notes { get; set; }
    public DateTime? FulfilledDate { get; set; }
    public DateTime? PickupDate { get; set; }

    // Foreign keys
    public Guid PatientId { get; set; }
    public Guid PrescriptionId { get; set; }
}

