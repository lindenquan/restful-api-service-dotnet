using Entities;

namespace Application.Orders.Shared;

/// <summary>
/// Shared mapper from Entity to Internal DTO.
/// Used by all handlers to convert domain entities to internal DTOs.
/// </summary>
public static class EntityToInternalDto
{
    /// <summary>
    /// Maps a PrescriptionOrder entity to an InternalOrderDto.
    /// Status is kept as enum for type safety - adapters convert to string if needed.
    /// </summary>
    public static InternalOrderDto Map(PrescriptionOrder order)
    {
        return new InternalOrderDto(
            Id: order.Id,
            PatientId: order.PatientId,
            PatientName: order.Patient?.FullName ?? "Unknown",
            PrescriptionId: order.PrescriptionId,
            MedicationName: order.Prescription?.MedicationName ?? "Unknown",
            Dosage: order.Prescription?.Dosage ?? "",
            OrderDate: order.OrderDate,
            Status: order.Status,  // ← Keep as enum, not converted to string
            Notes: order.Notes,
            FulfilledDate: order.FulfilledDate,
            PickupDate: order.PickupDate,
            CreatedAt: order.CreatedAt,
            UpdatedAt: order.UpdatedAt
        );
    }

    /// <summary>
    /// Maps a collection of PrescriptionOrder entities to InternalOrderDtos.
    /// </summary>
    public static IEnumerable<InternalOrderDto> MapMany(IEnumerable<PrescriptionOrder> orders)
    {
        return orders.Select(Map);
    }
}

