using Application.Orders.Operations;
using Application.Orders.Shared;
using DTOs.V1;
using Domain;

namespace Infrastructure.Api.Controllers.V1.Mappers;

/// <summary>
/// Maps between V1 DTOs and Commands/InternalDtos.
/// Commands serve as the internal representation - no separate input DTOs needed.
/// </summary>
public static class OrderMapper
{
    /// <summary>
    /// Maps internal DTO to V1 response DTO.
    /// Converts OrderStatus enum to string for external API.
    /// </summary>
    public static OrderDto ToV1Dto(InternalOrderDto internalDto)
    {
        return new OrderDto(
            Id: internalDto.Id,
            PatientId: internalDto.PatientId,
            CustomerName: internalDto.PatientName,   // V1 naming: CustomerName
            PrescriptionId: internalDto.PrescriptionId,
            Medication: internalDto.MedicationName,  // V1 naming: Medication
            OrderDate: internalDto.OrderDate,
            Status: internalDto.Status.ToString(),   // ← Convert enum to string for V1 API
            Notes: internalDto.Notes
        );
    }

    /// <summary>
    /// Maps V1 create request directly to Command.
    /// </summary>
    public static CreateOrderCommand ToCommand(CreateOrderRequest request, string? createdBy = null)
    {
        return new CreateOrderCommand(
            PatientId: request.PatientId,
            PrescriptionId: request.PrescriptionId,
            Notes: request.Notes,
            CreatedBy: createdBy
        );
    }

    /// <summary>
    /// Maps V1 update request directly to Command.
    /// V1 doesn't support updating notes.
    /// </summary>
    public static UpdateOrderStatusCommand ToCommand(int orderId, UpdateOrderRequest request, OrderStatus status, string? updatedBy = null)
    {
        return new UpdateOrderStatusCommand(
            OrderId: orderId,
            Status: status,
            Notes: null,  // V1 doesn't allow notes update
            UpdatedBy: updatedBy
        );
    }

    /// <summary>
    /// Maps a collection of internal DTOs to V1 response DTOs.
    /// </summary>
    public static IEnumerable<OrderDto> ToV1Dtos(IEnumerable<InternalOrderDto> internalDtos)
    {
        return internalDtos.Select(ToV1Dto);
    }
}

