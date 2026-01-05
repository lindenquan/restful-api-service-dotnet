using Application.Orders.Operations;
using Application.Orders.Shared;
using DTOs.V2;
using Domain;

namespace Infrastructure.Api.Controllers.V2.Mappers;

/// <summary>
/// Maps between V2 DTOs and Commands/InternalDtos.
/// Commands serve as the internal representation - no separate input DTOs needed.
/// </summary>
public static class PrescriptionOrderMapper
{
    /// <summary>
    /// Maps internal DTO to V2 response DTO.
    /// V2 includes all fields. Converts OrderStatus enum to string for external API.
    /// </summary>
    public static PrescriptionOrderDto ToV2Dto(InternalOrderDto internalDto)
    {
        return new PrescriptionOrderDto(
            Id: internalDto.Id,
            PatientId: internalDto.PatientId,
            PatientName: internalDto.PatientName,
            PrescriptionId: internalDto.PrescriptionId,
            MedicationName: internalDto.MedicationName,
            Dosage: internalDto.Dosage,
            OrderDate: internalDto.OrderDate,
            Status: internalDto.Status.ToString(),  // ← Convert enum to string for V2 API
            Notes: internalDto.Notes,
            FulfilledDate: internalDto.FulfilledDate,
            PickupDate: internalDto.PickupDate,
            CreatedAt: internalDto.CreatedAt,
            UpdatedAt: internalDto.UpdatedAt
        );
    }

    /// <summary>
    /// Maps V2 create request directly to Command.
    /// Note: IsUrgent is handled by adding to notes.
    /// </summary>
    public static CreateOrderCommand ToCommand(CreatePrescriptionOrderRequest request, string? createdBy = null)
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
    public static UpdateOrderStatusCommand ToCommand(int orderId, UpdatePrescriptionOrderRequest request, OrderStatus status, string? updatedBy = null)
    {
        return new UpdateOrderStatusCommand(
            OrderId: orderId,
            Status: status,
            Notes: request.Notes,
            UpdatedBy: updatedBy
        );
    }

    /// <summary>
    /// Maps a collection of internal DTOs to V2 response DTOs.
    /// </summary>
    public static IEnumerable<PrescriptionOrderDto> ToV2Dtos(IEnumerable<InternalOrderDto> internalDtos)
    {
        return internalDtos.Select(ToV2Dto);
    }
}

