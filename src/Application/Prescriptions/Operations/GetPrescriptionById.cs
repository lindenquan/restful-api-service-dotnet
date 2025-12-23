using Application.DTOs;
using Application.Interfaces.Repositories;
using Entities;
using MediatR;

namespace Application.Prescriptions.Operations;

/// <summary>
/// Query to get a prescription by ID.
/// </summary>
public record GetPrescriptionByIdQuery(int PrescriptionId) : IRequest<PrescriptionDto?>;

/// <summary>
/// Handler for GetPrescriptionByIdQuery.
/// </summary>
public class GetPrescriptionByIdHandler : IRequestHandler<GetPrescriptionByIdQuery, PrescriptionDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPrescriptionByIdHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PrescriptionDto?> Handle(GetPrescriptionByIdQuery request, CancellationToken ct)
    {
        var prescription = await _unitOfWork.Prescriptions.GetByIdWithPatientAsync(request.PrescriptionId, ct);
        if (prescription == null)
            return null;

        return MapToDto(prescription);
    }

    private static PrescriptionDto MapToDto(Prescription p) => new(
        p.Id,
        p.PatientId,
        p.Patient?.FullName ?? "Unknown",
        p.MedicationName,
        p.Dosage,
        p.Frequency,
        p.Quantity,
        p.RefillsRemaining,
        p.PrescriberName,
        p.PrescribedDate,
        p.ExpiryDate,
        p.Instructions,
        p.IsExpired,
        p.CanRefill,
        p.CreatedAt
    );
}

