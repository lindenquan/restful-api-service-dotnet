namespace DTOs.V1;

/// <summary>
/// V1 Order response DTO.
/// V1 uses simpler field names and fewer fields.
/// </summary>
public record OrderDto(
    Guid Id,
    Guid PatientId,
    string CustomerName,      // V1 calls it "CustomerName" (V2 calls it "PatientName")
    Guid PrescriptionId,
    string Medication,        // V1 calls it "Medication" (V2 calls it "MedicationName")
    DateTime OrderDate,
    string Status,
    string? Notes
);

/// <summary>
/// V1 Create Order request DTO.
/// </summary>
public record CreateOrderRequest(
    Guid PatientId,
    Guid PrescriptionId,
    string? Notes
);

/// <summary>
/// V1 Update Order request DTO.
/// V1 only allows updating status (no notes update).
/// </summary>
public record UpdateOrderRequest(
    string Status
);

