using Application.Interfaces.Repositories;
using Domain;
using DTOs.Shared;
using MediatR;

namespace Application.Patients.Operations;

/// <summary>
/// Query to get patients with OData-style pagination.
/// </summary>
public sealed record GetPatientsPagedQuery(
    int Skip,
    int Top,
    bool IncludeCount = false,
    string? OrderBy = null,
    bool Descending = false) : IRequest<PagedData<Patient>>;

/// <summary>
/// Handler for GetPatientsPagedQuery.
/// </summary>
public sealed class GetPatientsPagedHandler : IRequestHandler<GetPatientsPagedQuery, PagedData<Patient>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPatientsPagedHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedData<Patient>> Handle(GetPatientsPagedQuery request, CancellationToken ct)
    {
        return await _unitOfWork.Patients.GetPagedAsync(
            request.Skip,
            request.Top,
            request.IncludeCount,
            request.OrderBy,
            request.Descending,
            ct);
    }
}

