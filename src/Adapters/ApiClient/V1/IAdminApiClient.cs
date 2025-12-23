using Contracts.V1;
using DTOs.Shared;
using Refit;

namespace Adapters.ApiClient.V1;

/// <summary>
/// Refit-based HTTP client for Admin V1 API.
/// Implements IAdminApi via HTTP calls.
/// </summary>
public interface IAdminApiClient : IAdminApi
{
    /// <summary>
    /// Create a new API key user.
    /// </summary>
    [Post("/api/v1/admin/api-keys")]
    new Task<CreateUserResponse> CreateApiKeyAsync([Body] CreateUserRequest request, CancellationToken ct = default);
}

