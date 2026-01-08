using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using DTOs.Shared;
using MediatR;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Query to get prescriptions with OData filtering, sorting, and pagination.
/// </summary>
public sealed record GetPrescriptionsPagedQuery(
    int Skip,
    int Top,
    bool IncludeCount = false,
    string? OrderBy = null,
    bool Descending = false) : IRequest<PagedData<Prescription>>, ICacheableQuery
{
    public string CacheKey => $"prescriptions:paged:{Skip}:{Top}:{OrderBy}:{Descending}";
}

/// <summary>
/// Handler for GetPrescriptionsPagedQuery.
/// </summary>
public sealed class GetPrescriptionsPagedHandler : IRequestHandler<GetPrescriptionsPagedQuery, PagedData<Prescription>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPrescriptionsPagedHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedData<Prescription>> Handle(GetPrescriptionsPagedQuery request, CancellationToken ct)
    {
        return await _unitOfWork.Prescriptions.GetPagedWithPatientsAsync(
            request.Skip,
            request.Top,
            request.IncludeCount,
            request.OrderBy,
            request.Descending,
            ct);
    }
}

