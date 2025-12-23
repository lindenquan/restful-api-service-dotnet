using Application.Interfaces.Repositories;
using Application.Orders.Shared;
using Entities;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get prescription orders by status.
/// Uses internal DTO - controllers map to/from versioned DTOs.
/// </summary>
public record GetOrdersByStatusQuery(OrderStatus Status) : IRequest<IEnumerable<InternalOrderDto>>;

/// <summary>
/// Handler for GetOrdersByStatusQuery.
/// </summary>
public class GetOrdersByStatusHandler : IRequestHandler<GetOrdersByStatusQuery, IEnumerable<InternalOrderDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrdersByStatusHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<InternalOrderDto>> Handle(GetOrdersByStatusQuery request, CancellationToken ct)
    {
        var orders = await _unitOfWork.PrescriptionOrders.GetByStatusAsync(request.Status, ct);
        return EntityToInternalDto.MapMany(orders);
    }
}

