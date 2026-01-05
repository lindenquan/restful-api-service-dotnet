using Application.Interfaces.Repositories;
using Application.Orders.Shared;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Command to create a new prescription order.
/// Uses internal DTO - controllers map to/from versioned DTOs.
/// </summary>
public record CreateOrderCommand(
    int PatientId,
    int PrescriptionId,
    string? Notes,
    string? CreatedBy = null
) : IRequest<InternalOrderDto>;

/// <summary>
/// Handler for CreateOrderCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, InternalOrderDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<InternalOrderDto> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // Validate patient exists
        var patient = await _unitOfWork.Patients.GetByIdAsync(request.PatientId, ct)
            ?? throw new ArgumentException($"Patient with ID {request.PatientId} not found");

        // Validate prescription exists and can be ordered
        var prescription = await _unitOfWork.Prescriptions.GetByIdAsync(request.PrescriptionId, ct)
            ?? throw new ArgumentException($"Prescription with ID {request.PrescriptionId} not found");

        if (prescription.IsExpired)
            throw new InvalidOperationException("Cannot order an expired prescription");

        var order = new PrescriptionOrder
        {
            PatientId = request.PatientId,
            PrescriptionId = request.PrescriptionId,
            Notes = request.Notes,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Metadata = { CreatedBy = request.CreatedBy }
        };

        await _unitOfWork.PrescriptionOrders.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Reload with details
        var created = await _unitOfWork.PrescriptionOrders.GetByIdWithDetailsAsync(order.Id, ct);
        return EntityToInternalDto.Map(created!);
    }
}

