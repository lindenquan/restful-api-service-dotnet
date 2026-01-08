using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get a prescription order by ID.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record GetOrderByIdQuery(Guid OrderId) : IRequest<PrescriptionOrder?>, ICacheableQuery
{
    public string CacheKey => $"order:{OrderId}";
}

/// <summary>
/// Handler for GetOrderByIdQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, PrescriptionOrder?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrderByIdHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PrescriptionOrder?> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        return await _unitOfWork.PrescriptionOrders.GetByIdWithDetailsAsync(request.OrderId, ct);
    }
}

