namespace DTOs.V2;

/// <summary>
/// V2 Prescription response DTO.
/// V2 includes more detailed fields like status indicators and audit timestamps.
/// </summary>
public record PrescriptionDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsRemaining,
    string PrescriberName,
    DateTime PrescribedDate,
    DateTime ExpiryDate,
    string? Instructions,
    bool IsExpired,                   // V2 includes status indicators
    bool CanRefill,                   // V2 includes status indicators
    int DaysUntilExpiry,              // V2 includes calculated field (V1 doesn't)
    DateTime CreatedAt,               // V2 includes audit fields (V1 doesn't)
    DateTime? UpdatedAt
);

/// <summary>
/// V2 Create Prescription request DTO.
/// </summary>
public record CreatePrescriptionRequest(
    Guid PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions,
    bool IsControlledSubstance = false  // V2 supports controlled substance flag (V1 doesn't)
);

/// <summary>
/// V2 Refill Prescription request DTO.
/// V2 supports prescription refills (V1 doesn't).
/// </summary>
public record RefillPrescriptionRequest(
    int Quantity,
    string? Notes
);

