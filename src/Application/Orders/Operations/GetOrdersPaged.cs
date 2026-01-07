using Application.Interfaces.Repositories;
using Domain;
using DTOs.Shared;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Query to get prescription orders with OData filtering, sorting, and pagination.
/// </summary>
public sealed record GetOrdersPagedQuery(
    int Skip,
    int Top,
    bool IncludeCount = false,
    string? OrderBy = null,
    bool Descending = false) : IRequest<PagedData<PrescriptionOrder>>;

/// <summary>
/// Handler for GetOrdersPagedQuery.
/// </summary>
public sealed class GetOrdersPagedHandler : IRequestHandler<GetOrdersPagedQuery, PagedData<PrescriptionOrder>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrdersPagedHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedData<PrescriptionOrder>> Handle(GetOrdersPagedQuery request, CancellationToken ct)
    {
        return await _unitOfWork.PrescriptionOrders.GetPagedWithDetailsAsync(
            request.Skip,
            request.Top,
            request.IncludeCount,
            request.OrderBy,
            request.Descending,
            ct);
    }
}

/// <summary>
/// Query to get prescription orders by patient with OData filtering, sorting, and pagination.
/// </summary>
public sealed record GetOrdersByPatientPagedQuery(
    Guid PatientId,
    int Skip,
    int Top,
    bool IncludeCount = false,
    string? OrderBy = null,
    bool Descending = false) : IRequest<PagedData<PrescriptionOrder>>;

/// <summary>
/// Handler for GetOrdersByPatientPagedQuery.
/// </summary>
public sealed class GetOrdersByPatientPagedHandler : IRequestHandler<GetOrdersByPatientPagedQuery, PagedData<PrescriptionOrder>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrdersByPatientPagedHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedData<PrescriptionOrder>> Handle(GetOrdersByPatientPagedQuery request, CancellationToken ct)
    {
        return await _unitOfWork.PrescriptionOrders.GetPagedByPatientWithDetailsAsync(
            request.PatientId,
            request.Skip,
            request.Top,
            request.IncludeCount,
            request.OrderBy,
            request.Descending,
            ct);
    }
}

