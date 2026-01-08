using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using MediatR;

namespace Application.Patients.Operations;

/// <summary>
/// Command to create a new patient.
/// Controllers map to/from versioned DTOs.
/// </summary>
public record CreatePatientCommand(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime DateOfBirth
) : IRequest<Patient>, ICacheInvalidatingCommand
{
    public IEnumerable<string> CacheKeysToInvalidate =>
    [
        "patients:all",
        "patients:paged:*"
    ];
}

/// <summary>
/// Handler for CreatePatientCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreatePatientHandler : IRequestHandler<CreatePatientCommand, Patient>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreatePatientHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Patient> Handle(CreatePatientCommand request, CancellationToken ct)
    {
        // Check if email already exists
        var existingPatient = await _unitOfWork.Patients.GetByEmailAsync(request.Email, ct);
        if (existingPatient != null)
        {
            throw new ArgumentException($"A patient with email '{request.Email}' already exists");
        }

        var patient = new Patient
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth
        };

        await _unitOfWork.Patients.AddAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Reload patient to ensure we have the generated ID
        return (await _unitOfWork.Patients.GetByIdAsync(patient.Id, ct))!;
    }
}

