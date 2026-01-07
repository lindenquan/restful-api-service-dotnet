using Application.Patients.Operations;
using Domain;
using DTOs.V2;

namespace Infrastructure.Api.Controllers.V2.Mappers;

/// <summary>
/// Maps between V2 Patient DTOs and Commands/Domain entities.
/// </summary>
public static class PatientMapper
{
    /// <summary>
    /// Maps domain entity to V2 response DTO.
    /// V2 includes additional calculated fields like age.
    /// </summary>
    public static PatientDto ToV2Dto(Patient patient)
    {
        var age = CalculateAge(patient.DateOfBirth);

        return new PatientDto(
            Id: patient.Id,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            FullName: patient.FullName,
            Email: patient.Email,
            Phone: patient.Phone,
            DateOfBirth: patient.DateOfBirth,
            Age: age,
            CreatedAt: patient.CreatedAt,
            UpdatedAt: patient.UpdatedAt
        );
    }

    /// <summary>
    /// Maps V2 create request directly to Command.
    /// </summary>
    public static CreatePatientCommand ToCommand(CreatePatientRequest request)
    {
        return new CreatePatientCommand(
            FirstName: request.FirstName,
            LastName: request.LastName,
            Email: request.Email,
            Phone: request.Phone,
            DateOfBirth: request.DateOfBirth
        );
    }

    /// <summary>
    /// Maps a collection of domain entities to V2 response DTOs.
    /// </summary>
    public static IEnumerable<PatientDto> ToV2Dtos(IEnumerable<Patient> patients)
    {
        return patients.Select(ToV2Dto);
    }

    /// <summary>
    /// Calculate age from date of birth.
    /// </summary>
    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;
        return age;
    }
}

