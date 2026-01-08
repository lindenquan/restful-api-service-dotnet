using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Query to get a prescription by ID.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record GetPrescriptionByIdQuery(Guid PrescriptionId) : IRequest<Prescription?>, ICacheableQuery
{
    public string CacheKey => $"prescription:{PrescriptionId}";
}

/// <summary>
/// Handler for GetPrescriptionByIdQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetPrescriptionByIdHandler : IRequestHandler<GetPrescriptionByIdQuery, Prescription?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPrescriptionByIdHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Prescription?> Handle(GetPrescriptionByIdQuery request, CancellationToken ct)
    {
        return await _unitOfWork.Prescriptions.GetByIdWithPatientAsync(request.PrescriptionId, ct);
    }
}

