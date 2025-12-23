using Entities;

namespace Application.DTOs;

/// <summary>
/// Prescription Order DTO for API responses.
/// </summary>
public record PrescriptionOrderDto(
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
    DateTime CreatedAt
);

/// <summary>
/// DTO for creating a new prescription order.
/// </summary>
public record CreatePrescriptionOrderDto(
    int PatientId,
    int PrescriptionId,
    string? Notes
);

/// <summary>
/// DTO for updating a prescription order status.
/// </summary>
public record UpdatePrescriptionOrderDto(
    OrderStatus Status,
    string? Notes
);

/// <summary>
/// Patient DTO for API responses.
/// </summary>
public record PatientDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime DateOfBirth,
    DateTime CreatedAt
);

/// <summary>
/// DTO for creating a new patient.
/// </summary>
public record CreatePatientDto(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime DateOfBirth
);

/// <summary>
/// Prescription DTO for API responses.
/// </summary>
public record PrescriptionDto(
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

/// <summary>
/// DTO for creating a new prescription.
/// </summary>
public record CreatePrescriptionDto(
    int PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions
);

