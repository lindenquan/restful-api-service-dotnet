using Application.Interfaces.Repositories;
using Application.Orders.Shared;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get all prescription orders.
/// Uses internal DTO - controllers map to/from versioned DTOs.
/// </summary>
public record GetAllOrdersQuery : IRequest<IEnumerable<InternalOrderDto>>;

/// <summary>
/// Handler for GetAllOrdersQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetAllOrdersHandler : IRequestHandler<GetAllOrdersQuery, IEnumerable<InternalOrderDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllOrdersHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<InternalOrderDto>> Handle(GetAllOrdersQuery request, CancellationToken ct)
    {
        var orders = await _unitOfWork.PrescriptionOrders.GetAllAsync(ct);
        var result = new List<InternalOrderDto>();

        foreach (var order in orders)
        {
            var detailed = await _unitOfWork.PrescriptionOrders.GetByIdWithDetailsAsync(order.Id, ct);
            if (detailed != null)
                result.Add(EntityToInternalDto.Map(detailed));
        }

        return result;
    }
}

