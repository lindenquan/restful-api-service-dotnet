namespace Application.Prescriptions.Shared;

/// <summary>
/// Internal DTO used by Prescription handlers.
/// This is the "canonical" representation used internally for RESPONSES.
/// </summary>
public record InternalPrescriptionDto(
    int Id,
    int PatientId,
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
    bool IsExpired,
    bool CanRefill,
    DateTime CreatedAt
);

