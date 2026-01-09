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
        // Order cache keys
        "orders:all",
        "orders:paged:*",
        $"orders:patient:{PatientId}",
        $"orders:patient:{PatientId}:paged:*",
        "orders:status:*",
        // Prescription cache keys (refills are decremented)
        $"prescription:{PrescriptionId}",
        $"prescriptions:patient:{PatientId}",
        "prescriptions:paged:*"
    ];
}

/// <summary>
/// Handler for CreateOrderCommand.
/// <para>
/// This handler uses a transaction to ensure atomicity when:
/// 1. Reading prescription to check refills
/// 2. Decrementing refills remaining
/// 3. Creating the order
/// </para>
/// <para>
/// If any step fails, all changes are rolled back.
/// </para>
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
        // Use transaction to ensure atomicity of read-modify-write operations
        var orderId = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Validate patient exists
            _ = await _unitOfWork.Patients.GetByIdAsync(request.PatientId, ct)
                ?? throw new ArgumentException($"Patient with ID {request.PatientId} not found");

            // Validate prescription exists and can be ordered
            var prescription = await _unitOfWork.Prescriptions.GetByIdAsync(request.PrescriptionId, ct)
                ?? throw new ArgumentException($"Prescription with ID {request.PrescriptionId} not found");

            if (prescription.IsExpired)
                throw new InvalidOperationException("Cannot order an expired prescription");

            if (prescription.RefillsRemaining <= 0)
                throw new InvalidOperationException("Prescription has no refills remaining");

            // Decrement refills remaining (atomic within transaction)
            prescription.RefillsRemaining--;
            prescription.UpdatedBy = request.CreatedBy;
            await _unitOfWork.Prescriptions.UpdateAsync(prescription, ct);

            // Create the order
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

            return order.Id;
        }, ct);

        // Reload with details (outside transaction for fresh read)
        return (await _unitOfWork.PrescriptionOrders.GetByIdWithDetailsAsync(orderId, ct))!;
    }
}

