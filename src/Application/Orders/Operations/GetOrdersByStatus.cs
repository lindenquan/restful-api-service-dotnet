using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get prescription orders by status.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record GetOrdersByStatusQuery(OrderStatus Status) : IRequest<IEnumerable<PrescriptionOrder>>;

/// <summary>
/// Handler for GetOrdersByStatusQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetOrdersByStatusHandler : IRequestHandler<GetOrdersByStatusQuery, IEnumerable<PrescriptionOrder>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrdersByStatusHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<PrescriptionOrder>> Handle(GetOrdersByStatusQuery request, CancellationToken ct)
    {
        return await _unitOfWork.PrescriptionOrders.GetByStatusWithDetailsAsync(request.Status, ct);
    }
}

