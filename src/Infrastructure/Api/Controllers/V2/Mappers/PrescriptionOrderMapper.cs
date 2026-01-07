using Application.Orders.Operations;
using Domain;
using DTOs.V2;

namespace Infrastructure.Api.Controllers.V2.Mappers;

/// <summary>
/// Maps between V2 DTOs and Commands/Domain entities.
/// Commands serve as the internal representation - no separate input DTOs needed.
/// </summary>
public static class PrescriptionOrderMapper
{
    /// <summary>
    /// Maps domain entity to V2 response DTO.
    /// V2 includes all fields. Converts OrderStatus enum to string for external API.
    /// </summary>
    public static PrescriptionOrderDto ToV2Dto(PrescriptionOrder order)
    {
        return new PrescriptionOrderDto(
            Id: order.Id,
            PatientId: order.PatientId,
            PatientName: order.Patient?.FullName ?? "Unknown",
            PrescriptionId: order.PrescriptionId,
            MedicationName: order.Prescription?.MedicationName ?? "Unknown",
            Dosage: order.Prescription?.Dosage ?? "",
            OrderDate: order.OrderDate,
            Status: order.Status.ToString(),  // ← Convert enum to string for V2 API
            Notes: order.Notes,
            FulfilledDate: order.FulfilledDate,
            PickupDate: order.PickupDate,
            CreatedAt: order.CreatedAt,
            UpdatedAt: order.UpdatedAt
        );
    }

    /// <summary>
    /// Maps V2 create request directly to Command.
    /// Note: IsUrgent is handled by adding to notes.
    /// </summary>
    public static CreateOrderCommand ToCommand(CreatePrescriptionOrderRequest request, Guid? createdBy = null)
    {
        var notes = request.Notes;
        if (request.IsUrgent)
        {
            notes = $"[URGENT] {notes ?? ""}".Trim();
        }

        return new CreateOrderCommand(
            PatientId: request.PatientId,
            PrescriptionId: request.PrescriptionId,
            Notes: notes,
            CreatedBy: createdBy
        );
    }

    /// <summary>
    /// Maps V2 update request directly to Command.
    /// V2 supports updating notes.
    /// </summary>
    public static UpdateOrderStatusCommand ToCommand(Guid orderId, UpdatePrescriptionOrderRequest request, OrderStatus status, Guid? updatedBy = null)
    {
        return new UpdateOrderStatusCommand(
            OrderId: orderId,
            Status: status,
            Notes: request.Notes,
            UpdatedBy: updatedBy
        );
    }

    /// <summary>
    /// Maps a collection of domain entities to V2 response DTOs.
    /// </summary>
    public static IEnumerable<PrescriptionOrderDto> ToV2Dtos(IEnumerable<PrescriptionOrder> orders)
    {
        return orders.Select(ToV2Dto);
    }
}

