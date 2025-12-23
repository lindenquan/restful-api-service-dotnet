using DTOs.Shared;

namespace Contracts.V1;

/// <summary>
/// Pure API contract for Admin V1.
/// No ASP.NET Core dependencies - can be used by any client.
/// </summary>
public interface IAdminApi
{
    /// <summary>
    /// Create a new API key user.
    /// </summary>
    Task<CreateUserResponse> CreateApiKeyAsync(CreateUserRequest request, CancellationToken ct = default);
}

