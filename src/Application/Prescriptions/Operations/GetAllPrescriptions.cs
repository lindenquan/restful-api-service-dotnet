using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Query to get all prescriptions.
/// </summary>
public record GetAllPrescriptionsQuery : IRequest<IEnumerable<Prescription>>, ICacheableQuery
{
    public string CacheKey => "prescriptions:all";
}

/// <summary>
/// Handler for GetAllPrescriptionsQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetAllPrescriptionsHandler : IRequestHandler<GetAllPrescriptionsQuery, IEnumerable<Prescription>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllPrescriptionsHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Prescription>> Handle(GetAllPrescriptionsQuery request, CancellationToken ct)
    {
        return await _unitOfWork.Prescriptions.GetAllWithPatientsAsync(ct);
    }
}

