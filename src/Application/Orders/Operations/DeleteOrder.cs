using Application.Interfaces;
using Application.Interfaces.Repositories;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Command to delete a prescription order.
/// Supports both soft delete (default) and hard delete (admin only).
/// </summary>
/// <param name="OrderId">ID of the order to delete</param>
/// <param name="HardDelete">If true, permanently delete. If false, soft delete.</param>
/// <param name="DeletedBy">User ID of who is deleting</param>
public record DeleteOrderCommand(
    Guid OrderId,
    bool HardDelete = false,
    Guid? DeletedBy = null) : IRequest<bool>, ICacheInvalidatingCommand
{
    public IEnumerable<string> CacheKeysToInvalidate =>
    [
        $"order:{OrderId}",
        "orders:all",
        "orders:paged:*",
        "orders:patient:*",
        "orders:status:*"
    ];
}

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
            await _unitOfWork.PrescriptionOrders.HardDeleteAsync(order, ct);
        }
        else
        {
            // Soft delete - mark as deleted
            await _unitOfWork.PrescriptionOrders.SoftDeleteAsync(order, request.DeletedBy, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

