using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Command to update prescription order status.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatus Status,
    string? Notes,
    Guid? UpdatedBy = null
) : IRequest<PrescriptionOrder?>;

/// <summary>
/// Handler for UpdateOrderStatusCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class UpdateOrderStatusHandler : IRequestHandler<UpdateOrderStatusCommand, PrescriptionOrder?>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrderStatusHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PrescriptionOrder?> Handle(UpdateOrderStatusCommand request, CancellationToken ct)
    {
        var order = await _unitOfWork.PrescriptionOrders.GetByIdAsync(request.OrderId, ct);
        if (order == null)
            return null;

        order.Status = request.Status;
        if (request.Notes != null)
            order.Notes = request.Notes;
        order.UpdatedBy = request.UpdatedBy;

        // Set fulfilled/pickup dates based on status
        if (request.Status == OrderStatus.Ready)
            order.FulfilledDate = DateTime.UtcNow;
        else if (request.Status == OrderStatus.Completed)
            order.PickupDate = DateTime.UtcNow;

        _unitOfWork.PrescriptionOrders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);

        return await _unitOfWork.PrescriptionOrders.GetByIdWithDetailsAsync(request.OrderId, ct);
    }
}

