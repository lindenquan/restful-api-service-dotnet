using Application.Interfaces.Repositories;
using Application.Prescriptions.Shared;
using MediatR;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Query to get a prescription by ID.
/// Uses internal DTO - controllers map to/from versioned DTOs.
/// </summary>
public record GetPrescriptionByIdQuery(int PrescriptionId) : IRequest<InternalPrescriptionDto?>;

/// <summary>
/// Handler for GetPrescriptionByIdQuery.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class GetPrescriptionByIdHandler : IRequestHandler<GetPrescriptionByIdQuery, InternalPrescriptionDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPrescriptionByIdHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<InternalPrescriptionDto?> Handle(GetPrescriptionByIdQuery request, CancellationToken ct)
    {
        var prescription = await _unitOfWork.Prescriptions.GetByIdWithPatientAsync(request.PrescriptionId, ct);
        return prescription == null ? null : EntityToInternalDto.Map(prescription);
    }
}

