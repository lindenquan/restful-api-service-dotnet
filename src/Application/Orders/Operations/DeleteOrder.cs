using Application.Interfaces.Repositories;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Command to delete a prescription order.
/// Supports both soft delete (default) and hard delete (admin only).
/// </summary>
/// <param name="OrderId">ID of the order to delete</param>
/// <param name="HardDelete">If true, permanently delete. If false, soft delete.</param>
/// <param name="DeletedBy">Username of who is deleting</param>
public record DeleteOrderCommand(
    int OrderId,
    bool HardDelete = false,
    string? DeletedBy = null) : IRequest<bool>;

/// <summary>
/// Handler for DeleteOrderCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class DeleteOrderHandler : IRequestHandler<DeleteOrderCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteOrderHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteOrderCommand request, CancellationToken ct)
    {
        var order = await _unitOfWork.PrescriptionOrders.GetByIdAsync(request.OrderId, ct);
        if (order == null)
            return false;

        if (request.HardDelete)
        {
            // Permanently delete from database
            _unitOfWork.PrescriptionOrders.HardDelete(order);
        }
        else
        {
            // Soft delete - mark as deleted
            _unitOfWork.PrescriptionOrders.SoftDelete(order, request.DeletedBy);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

