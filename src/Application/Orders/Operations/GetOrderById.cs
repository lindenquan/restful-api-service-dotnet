using Application.Interfaces.Repositories;
using Application.Orders.Shared;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get a prescription order by ID.
/// Uses internal DTO - controllers map to/from versioned DTOs.
/// </summary>
public record GetOrderByIdQuery(int OrderId) : IRequest<InternalOrderDto?>;

/// <summary>
/// Handler for GetOrderByIdQuery.
/// </summary>
public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, InternalOrderDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrderByIdHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<InternalOrderDto?> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        var order = await _unitOfWork.PrescriptionOrders.GetByIdWithDetailsAsync(request.OrderId, ct);
        return order == null ? null : EntityToInternalDto.Map(order);
    }
}

