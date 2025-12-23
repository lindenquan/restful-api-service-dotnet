namespace Entities;

/// <summary>
/// Patient entity representing a patient in the system.
/// </summary>
public class Patient : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime DateOfBirth { get; set; }

    // Navigation properties
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public ICollection<PrescriptionOrder> Orders { get; set; } = new List<PrescriptionOrder>();

    // Computed property
    public string FullName => $"{FirstName} {LastName}";
}

