using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Command to cancel a prescription order.
/// </summary>
public record CancelOrderCommand(Guid OrderId) : IRequest<bool>;

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
        _unitOfWork.PrescriptionOrders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

