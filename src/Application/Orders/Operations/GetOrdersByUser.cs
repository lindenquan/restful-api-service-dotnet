using Application.Interfaces.Repositories;
using Application.Orders.Shared;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get prescription orders by patient ID.
/// Uses internal DTO - controllers map to/from versioned DTOs.
/// </summary>
public record GetOrdersByPatientQuery(int PatientId) : IRequest<IEnumerable<InternalOrderDto>>;

/// <summary>
/// Handler for GetOrdersByPatientQuery.
/// </summary>
public class GetOrdersByPatientHandler : IRequestHandler<GetOrdersByPatientQuery, IEnumerable<InternalOrderDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrdersByPatientHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<InternalOrderDto>> Handle(GetOrdersByPatientQuery request, CancellationToken ct)
    {
        var orders = await _unitOfWork.PrescriptionOrders.GetByPatientIdAsync(request.PatientId, ct);
        return EntityToInternalDto.MapMany(orders);
    }
}

