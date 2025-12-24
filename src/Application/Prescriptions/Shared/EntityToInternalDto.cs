using Entities;

namespace Application.Prescriptions.Shared;

/// <summary>
/// Shared mapper from Entity to Internal DTO.
/// Used by all handlers to convert domain entities to internal DTOs.
/// </summary>
public static class EntityToInternalDto
{
    /// <summary>
    /// Maps a Prescription entity to an InternalPrescriptionDto.
    /// </summary>
    public static InternalPrescriptionDto Map(Prescription prescription)
    {
        return new InternalPrescriptionDto(
            Id: prescription.Id,
            PatientId: prescription.PatientId,
            PatientName: prescription.Patient?.FullName ?? "Unknown",
            MedicationName: prescription.MedicationName,
            Dosage: prescription.Dosage,
            Frequency: prescription.Frequency,
            Quantity: prescription.Quantity,
            RefillsRemaining: prescription.RefillsRemaining,
            PrescriberName: prescription.PrescriberName,
            PrescribedDate: prescription.PrescribedDate,
            ExpiryDate: prescription.ExpiryDate,
            Instructions: prescription.Instructions,
            IsExpired: prescription.IsExpired,
            CanRefill: prescription.CanRefill,
            CreatedAt: prescription.Metadata.CreatedAt
        );
    }

    /// <summary>
    /// Maps a collection of Prescription entities to InternalPrescriptionDtos.
    /// </summary>
    public static IEnumerable<InternalPrescriptionDto> MapMany(IEnumerable<Prescription> prescriptions)
    {
        return prescriptions.Select(Map);
    }
}

