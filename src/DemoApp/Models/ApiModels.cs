namespace DemoApp.Models;

// Patient Models
public record PatientDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    DateTime DateOfBirth,
    int Age,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreatePatientRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime DateOfBirth
);

// Prescription Models
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
    bool IsExpired,
    bool CanRefill,
    int DaysUntilExpiry,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreatePrescriptionRequest(
    Guid PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions
);

// Order Models
public record OrderDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid PrescriptionId,
    string MedicationName,
    string Dosage,
    DateTime OrderDate,
    string Status,
    string ShippingAddress,
    string? Notes,
    DateTime? FulfilledDate,
    DateTime? PickupDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateOrderRequest(
    Guid PrescriptionId,
    string ShippingAddress,
    string? Notes
);

public record UpdateOrderRequest(
    string Status,
    string ShippingAddress,
    string? Notes
);

// Paged Result for OData responses
public record PagedResult<T>
{
    public List<T> Value { get; init; } = [];
    public int? ODataCount { get; init; }
    public string? ODataNextLink { get; init; }
}

// Health Check Response
public record HealthCheckResponse
{
    public string Status { get; init; } = "";
    public List<HealthCheckEntry> Checks { get; init; } = [];
}

public record HealthCheckEntry
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public string? Description { get; init; }
    public double Duration { get; init; }
}

// API Settings
public class ApiSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string ApiKey { get; set; } = "root-api-key-change-in-production-12345";
    public string ApiVersion { get; set; } = "2";
}

