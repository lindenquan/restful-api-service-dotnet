using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Orders.Operations;

/// <summary>
/// Command to create a new prescription order.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record CreateOrderCommand(
    Guid PatientId,
    Guid PrescriptionId,
    string? Notes,
    Guid? CreatedBy = null
) : IRequest<PrescriptionOrder>, ICacheInvalidatingCommand
{
    public IEnumerable<string> CacheKeysToInvalidate =>
    [
        "orders:all",
        "orders:paged:*",
        $"orders:patient:{PatientId}",
        $"orders:patient:{PatientId}:paged:*",
        "orders:status:*"
    ];
}

/// <summary>
/// Handler for CreateOrderCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, PrescriptionOrder>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PrescriptionOrder> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // Validate patient exists
        _ = await _unitOfWork.Patients.GetByIdAsync(request.PatientId, ct)
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
            CreatedBy = request.CreatedBy
        };

        await _unitOfWork.PrescriptionOrders.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Reload with details
        return (await _unitOfWork.PrescriptionOrders.GetByIdWithDetailsAsync(order.Id, ct))!;
    }
}

