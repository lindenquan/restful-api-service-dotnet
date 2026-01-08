using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Patients.Operations;

/// <summary>
/// Query to get all patients.
/// </summary>
public record GetAllPatientsQuery : IRequest<IEnumerable<Patient>>, ICacheableQuery
{
    public string CacheKey => "patients:all";
}

/// <summary>
/// Handler for GetAllPatientsQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetAllPatientsHandler : IRequestHandler<GetAllPatientsQuery, IEnumerable<Patient>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllPatientsHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Patient>> Handle(GetAllPatientsQuery request, CancellationToken ct)
    {
        return await _unitOfWork.Patients.GetAllAsync(ct);
    }
}

