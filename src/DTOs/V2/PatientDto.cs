namespace DTOs.V2;

/// <summary>
/// V2 Patient response DTO.
/// V2 includes more detailed fields like age calculation and audit timestamps.
/// </summary>
public record PatientDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    DateTime DateOfBirth,
    int Age,                          // V2 includes calculated age (V1 doesn't)
    DateTime CreatedAt,               // V2 includes audit fields (V1 doesn't)
    DateTime? UpdatedAt
);

/// <summary>
/// V2 Create Patient request DTO.
/// V2 supports additional optional fields.
/// </summary>
public record CreatePatientRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime DateOfBirth,
    string? Notes = null              // V2 supports notes (V1 doesn't)
);

/// <summary>
/// V2 Update Patient request DTO.
/// V2 allows updating patient information.
/// </summary>
public record UpdatePatientRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone
);

