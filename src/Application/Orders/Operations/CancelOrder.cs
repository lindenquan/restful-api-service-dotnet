using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Command to cancel a prescription order.
/// </summary>
public record CancelOrderCommand(Guid OrderId) : IRequest<bool>, ICacheInvalidatingCommand
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
/// Handler for CancelOrderCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CancelOrderHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public CancelOrderHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        var order = await _unitOfWork.PrescriptionOrders.GetByIdAsync(request.OrderId, ct);
        if (order == null)
            return false;

        if (order.Status == OrderStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed order");

        order.Status = OrderStatus.Cancelled;
        await _unitOfWork.PrescriptionOrders.UpdateAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

