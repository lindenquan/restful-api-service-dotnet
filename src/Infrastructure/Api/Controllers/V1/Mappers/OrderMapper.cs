using Application.Orders.Operations;
using Domain;
using DTOs.V1;

namespace Infrastructure.Api.Controllers.V1.Mappers;

/// <summary>
/// Maps between V1 DTOs and Commands/Domain entities.
/// Commands serve as the internal representation - no separate input DTOs needed.
/// </summary>
public static class OrderMapper
{
    /// <summary>
    /// Maps domain entity to V1 response DTO.
    /// Converts OrderStatus enum to string for external API.
    /// </summary>
    public static OrderDto ToV1Dto(PrescriptionOrder order)
    {
        return new OrderDto(
            Id: order.Id,
            PatientId: order.PatientId,
            CustomerName: order.Patient?.FullName ?? "Unknown",   // V1 naming: CustomerName
            PrescriptionId: order.PrescriptionId,
            Medication: order.Prescription?.MedicationName ?? "Unknown",  // V1 naming: Medication
            OrderDate: order.OrderDate,
            Status: order.Status.ToString(),   // ← Convert enum to string for V1 API
            Notes: order.Notes
        );
    }

    /// <summary>
    /// Maps V1 create request directly to Command.
    /// </summary>
    public static CreateOrderCommand ToCommand(CreateOrderRequest request, Guid? createdBy = null)
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
    public static UpdateOrderStatusCommand ToCommand(Guid orderId, UpdateOrderRequest request, OrderStatus status, Guid? updatedBy = null)
    {
        return new UpdateOrderStatusCommand(
            OrderId: orderId,
            Status: status,
            Notes: null,  // V1 doesn't allow notes update
            UpdatedBy: updatedBy
        );
    }

    /// <summary>
    /// Maps a collection of domain entities to V1 response DTOs.
    /// </summary>
    public static IEnumerable<OrderDto> ToV1Dtos(IEnumerable<PrescriptionOrder> orders)
    {
        return orders.Select(ToV1Dto);
    }
}

