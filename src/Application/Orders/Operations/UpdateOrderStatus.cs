using Application.Interfaces.Repositories;
using Application.Orders.Shared;
using Entities;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Command to update prescription order status.
/// Uses internal DTO - controllers map to/from versioned DTOs.
/// </summary>
public record UpdateOrderStatusCommand(
    int OrderId,
    OrderStatus Status,
    string? Notes,
    string? UpdatedBy = null
) : IRequest<InternalOrderDto?>;

/// <summary>
/// Handler for UpdateOrderStatusCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class UpdateOrderStatusHandler : IRequestHandler<UpdateOrderStatusCommand, InternalOrderDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrderStatusHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<InternalOrderDto?> Handle(UpdateOrderStatusCommand request, CancellationToken ct)
    {
        var order = await _unitOfWork.PrescriptionOrders.GetByIdAsync(request.OrderId, ct);
        if (order == null)
            return null;

        order.Status = request.Status;
        if (request.Notes != null)
            order.Notes = request.Notes;
        order.Metadata.UpdatedBy = request.UpdatedBy;

        // Set fulfilled/pickup dates based on status
        if (request.Status == OrderStatus.Ready)
            order.FulfilledDate = DateTime.UtcNow;
        else if (request.Status == OrderStatus.Completed)
            order.PickupDate = DateTime.UtcNow;

        _unitOfWork.PrescriptionOrders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _unitOfWork.PrescriptionOrders.GetByIdWithDetailsAsync(request.OrderId, ct);
        return EntityToInternalDto.Map(updated!);
    }
}

