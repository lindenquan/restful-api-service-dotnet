using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get all prescription orders.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record GetAllOrdersQuery : IRequest<IEnumerable<PrescriptionOrder>>, ICacheableQuery
{
    public string CacheKey => "orders:all";
}

/// <summary>
/// Handler for GetAllOrdersQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetAllOrdersHandler : IRequestHandler<GetAllOrdersQuery, IEnumerable<PrescriptionOrder>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllOrdersHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<PrescriptionOrder>> Handle(GetAllOrdersQuery request, CancellationToken ct)
    {
        return await _unitOfWork.PrescriptionOrders.GetAllWithDetailsAsync(ct);
    }
}

