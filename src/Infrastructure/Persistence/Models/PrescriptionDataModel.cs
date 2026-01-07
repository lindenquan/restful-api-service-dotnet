namespace Infrastructure.Persistence.Models;

/// <summary>
/// MongoDB data model for Prescription.
/// Contains domain data plus infrastructure metadata.
/// </summary>
public class PrescriptionDataModel : BaseDataModel
{
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int RefillsRemaining { get; set; }
    public string PrescriberName { get; set; } = string.Empty;
    public DateTime PrescribedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? Instructions { get; set; }

    // Foreign key
    public Guid PatientId { get; set; }
}

