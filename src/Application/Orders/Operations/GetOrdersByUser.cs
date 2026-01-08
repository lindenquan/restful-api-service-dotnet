using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get prescription orders by patient ID.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record GetOrdersByPatientQuery(Guid PatientId) : IRequest<IEnumerable<PrescriptionOrder>>, ICacheableQuery
{
    public string CacheKey => $"orders:patient:{PatientId}";
}

/// <summary>
/// Handler for GetOrdersByPatientQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetOrdersByPatientHandler : IRequestHandler<GetOrdersByPatientQuery, IEnumerable<PrescriptionOrder>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrdersByPatientHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<PrescriptionOrder>> Handle(GetOrdersByPatientQuery request, CancellationToken ct)
    {
        return await _unitOfWork.PrescriptionOrders.GetByPatientIdWithDetailsAsync(request.PatientId, ct);
    }
}

