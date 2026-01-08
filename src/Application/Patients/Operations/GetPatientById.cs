using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Patients.Operations;

/// <summary>
/// Query to get a patient by ID.
/// </summary>
public record GetPatientByIdQuery(Guid Id) : IRequest<Patient?>, ICacheableQuery
{
    public string CacheKey => $"patient:{Id}";
}

/// <summary>
/// Handler for GetPatientByIdQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetPatientByIdHandler : IRequestHandler<GetPatientByIdQuery, Patient?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPatientByIdHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Patient?> Handle(GetPatientByIdQuery request, CancellationToken ct)
    {
        return await _unitOfWork.Patients.GetByIdAsync(request.Id, ct);
    }
}

