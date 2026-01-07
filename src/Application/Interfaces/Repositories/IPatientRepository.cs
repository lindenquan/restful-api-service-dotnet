using Domain;

namespace Application.Interfaces.Repositories;

/// <summary>
/// Patient-specific repository interface.
/// </summary>
public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Patient?> GetByIdWithPrescriptionsAsync(Guid id, CancellationToken ct = default);
    Task<Patient?> GetByIdWithOrdersAsync(Guid id, CancellationToken ct = default);
}

