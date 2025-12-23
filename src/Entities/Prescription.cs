namespace Entities;

/// <summary>
/// Prescription entity representing a medical prescription.
/// </summary>
public class Prescription : BaseEntity
{
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;  // e.g., "500mg"
    public string Frequency { get; set; } = string.Empty;  // e.g., "Twice daily"
    public int Quantity { get; set; }  // Number of pills/units
    public int RefillsRemaining { get; set; }
    public string PrescriberName { get; set; } = string.Empty;  // Doctor's name
    public DateTime PrescribedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? Instructions { get; set; }  // Special instructions

    // Foreign key - the patient this prescription is for
    public int PatientId { get; set; }

    // Navigation properties
    public Patient? Patient { get; set; }
    public ICollection<PrescriptionOrder> Orders { get; set; } = new List<PrescriptionOrder>();

    // Computed properties
    public bool IsExpired => DateTime.UtcNow > ExpiryDate;
    public bool CanRefill => RefillsRemaining > 0 && !IsExpired;
}

