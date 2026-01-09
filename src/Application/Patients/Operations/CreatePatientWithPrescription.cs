using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Patients.Operations;

/// <summary>
/// Command to create a new patient with their initial prescription in a single atomic transaction.
/// This demonstrates MongoDB transaction support - both entities are created together or neither is created.
/// </summary>
/// <remarks>
/// <para><strong>Transaction Requirements:</strong></para>
/// <para>MongoDB transactions require a replica set or sharded cluster.</para>
/// <para>For local development, start MongoDB with --replSet option.</para>
/// </remarks>
public record CreatePatientWithPrescriptionCommand(
    // Patient fields
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string Email,
    string? Phone,
    // Prescription fields
    string MedicationName,
    string Dosage,
    string Frequency,
    int Quantity,
    int RefillsAllowed,
    string PrescriberName,
    DateTime ExpiryDate,
    string? Instructions,
    // Audit
    Guid? CreatedBy = null
) : IRequest<(Patient Patient, Prescription Prescription)>, ICacheInvalidatingCommand
{
    public IEnumerable<string> CacheKeysToInvalidate =>
    [
        "patients:all",
        "patients:paged:*",
        "prescriptions:all",
        "prescriptions:paged:*"
    ];
}

/// <summary>
/// Handler for CreatePatientWithPrescriptionCommand.
/// Demonstrates MongoDB transaction support by creating both entities atomically.
/// </summary>
/// <remarks>
/// <para><strong>Transaction Behavior:</strong></para>
/// <para>1. Begin transaction</para>
/// <para>2. Create patient</para>
/// <para>3. Create prescription linked to patient</para>
/// <para>4. Commit transaction (or rollback on error)</para>
/// <para>
/// If any step fails, the entire operation is rolled back.
/// This ensures data consistency - you'll never have a patient without their initial prescription
/// or a prescription referencing a non-existent patient.
/// </para>
/// </remarks>
public sealed class CreatePatientWithPrescriptionHandler
    : IRequestHandler<CreatePatientWithPrescriptionCommand, (Patient Patient, Prescription Prescription)>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreatePatientWithPrescriptionHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<(Patient Patient, Prescription Prescription)> Handle(
        CreatePatientWithPrescriptionCommand request,
        CancellationToken ct)
    {
        // Check for duplicate email before starting transaction
        var existingPatient = await _unitOfWork.Patients.GetByEmailAsync(request.Email, ct);
        if (existingPatient != null)
        {
            throw new InvalidOperationException($"A patient with email '{request.Email}' already exists");
        }

        // Create entities
        var patient = new Patient
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Email = request.Email,
            Phone = request.Phone,
            CreatedBy = request.CreatedBy
        };

        // Begin transaction - all subsequent operations are atomic
        await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            // Step 1: Create patient
            await _unitOfWork.Patients.AddAsync(patient, ct);

            // Step 2: Create prescription linked to the new patient
            var prescription = new Prescription
            {
                PatientId = patient.Id, // UUID v7 was generated during AddAsync
                MedicationName = request.MedicationName,
                Dosage = request.Dosage,
                Frequency = request.Frequency,
                Quantity = request.Quantity,
                RefillsRemaining = request.RefillsAllowed,
                PrescriberName = request.PrescriberName,
                PrescribedDate = DateTime.UtcNow,
                ExpiryDate = request.ExpiryDate,
                Instructions = request.Instructions,
                CreatedBy = request.CreatedBy
            };

            await _unitOfWork.Prescriptions.AddAsync(prescription, ct);

            // Step 3: Commit transaction - makes both entities permanent
            await _unitOfWork.CommitTransactionAsync(ct);

            // Return both created entities
            prescription.Patient = patient;
            patient.Prescriptions = [prescription];

            return (patient, prescription);
        }
        catch
        {
            // Rollback on any error - neither entity will be persisted
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}

