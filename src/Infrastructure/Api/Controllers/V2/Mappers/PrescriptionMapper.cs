using Application.Prescriptions.Operations;
using Domain;
using DTOs.V2;

namespace Infrastructure.Api.Controllers.V2.Mappers;

/// <summary>
/// Maps between V2 Prescription DTOs and Commands/Domain entities.
/// </summary>
public static class PrescriptionMapper
{
    /// <summary>
    /// Maps domain entity to V2 response DTO.
    /// V2 includes additional calculated fields like DaysUntilExpiry.
    /// </summary>
    public static PrescriptionDto ToV2Dto(Prescription prescription)
    {
        var daysUntilExpiry = (int)(prescription.ExpiryDate.Date - DateTime.UtcNow.Date).TotalDays;

        return new PrescriptionDto(
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
            DaysUntilExpiry: daysUntilExpiry,
            CreatedAt: prescription.CreatedAt,
            UpdatedAt: prescription.UpdatedAt
        );
    }

    /// <summary>
    /// Maps V2 create request directly to Command.
    /// </summary>
    public static CreatePrescriptionCommand ToCommand(CreatePrescriptionRequest request)
    {
        var instructions = request.Instructions;
        if (request.IsControlledSubstance)
        {
            instructions = $"[CONTROLLED SUBSTANCE] {instructions ?? ""}".Trim();
        }

        return new CreatePrescriptionCommand(
            PatientId: request.PatientId,
            MedicationName: request.MedicationName,
            Dosage: request.Dosage,
            Frequency: request.Frequency,
            Quantity: request.Quantity,
            RefillsAllowed: request.RefillsAllowed,
            PrescriberName: request.PrescriberName,
            ExpiryDate: request.ExpiryDate,
            Instructions: instructions
        );
    }

    /// <summary>
    /// Maps a collection of domain entities to V2 response DTOs.
    /// </summary>
    public static IEnumerable<PrescriptionDto> ToV2Dtos(IEnumerable<Prescription> prescriptions)
    {
        return prescriptions.Select(ToV2Dto);
    }
}

