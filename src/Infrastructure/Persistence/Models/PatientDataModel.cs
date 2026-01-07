namespace Infrastructure.Persistence.Models;

/// <summary>
/// MongoDB data model for Patient.
/// Contains domain data plus infrastructure metadata.
/// </summary>
public class PatientDataModel : BaseDataModel
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateTime DateOfBirth { get; set; }
}

