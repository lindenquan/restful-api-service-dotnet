using Domain;

namespace Application.Interfaces.Repositories;

/// <summary>
/// Patient-specific repository interface.
/// </summary>
public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Patient?> GetByIdWithPrescriptionsAsync(int id, CancellationToken ct = default);
    Task<Patient?> GetByIdWithOrdersAsync(int id, CancellationToken ct = default);
}

