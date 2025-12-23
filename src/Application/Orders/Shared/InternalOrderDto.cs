namespace Application.Orders.Shared;

/// <summary>
/// Internal DTO used by handlers - both V1 and V2 map to/from this.
/// This is the "canonical" representation used internally for RESPONSES.
///
/// For COMMANDS, the Command record itself (e.g., CreateOrderCommand)
/// serves as the internal representation - no separate DTO needed.
/// </summary>
public record InternalOrderDto(
    int Id,
    int PatientId,
    string PatientName,
    int PrescriptionId,
    string MedicationName,
    string Dosage,
    DateTime OrderDate,
    string Status,
    string? Notes,
    DateTime? FulfilledDate,
    DateTime? PickupDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

