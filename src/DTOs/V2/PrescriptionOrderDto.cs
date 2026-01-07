namespace DTOs.V2;

/// <summary>
/// V2 PrescriptionOrder response DTO.
/// V2 uses more descriptive names and includes additional fields.
/// </summary>
public record PrescriptionOrderDto(
    Guid Id,
    Guid PatientId,
    string PatientName,           // V2 naming: PatientName (V1 calls it CustomerName)
    Guid PrescriptionId,
    string MedicationName,        // V2 naming: MedicationName (V1 calls it Medication)
    string Dosage,                // V2 includes dosage (V1 doesn't)
    DateTime OrderDate,
    string Status,
    string? Notes,
    DateTime? FulfilledDate,      // V2 includes fulfillment tracking (V1 doesn't)
    DateTime? PickupDate,         // V2 includes pickup tracking (V1 doesn't)
    DateTime CreatedAt,           // V2 includes audit fields (V1 doesn't)
    DateTime? UpdatedAt
);

/// <summary>
/// V2 Create PrescriptionOrder request DTO.
/// </summary>
public record CreatePrescriptionOrderRequest(
    Guid PatientId,
    Guid PrescriptionId,
    string? Notes,
    bool IsUrgent = false         // V2 supports urgency flag (V1 doesn't)
);

/// <summary>
/// V2 Update PrescriptionOrder request DTO.
/// V2 allows updating both status and notes.
/// </summary>
public record UpdatePrescriptionOrderRequest(
    string Status,
    string? Notes                 // V2 allows notes update (V1 doesn't)
);

