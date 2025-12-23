using Entities;

namespace DTOs.Shared;

/// <summary>
/// Request to create a new user (API key user).
/// Shared across all API versions.
/// </summary>
public record CreateUserRequest(
    string UserName,
    string Email,
    UserType UserType,
    string? Description = null);

/// <summary>
/// Response containing the newly created user and API key.
/// WARNING: The ApiKey is shown ONLY in this response!
/// Shared across all API versions.
/// </summary>
public record CreateUserResponse(
    int UserId,
    string ApiKey,
    string ApiKeyPrefix,
    string UserName,
    string Email,
    string UserType,
    string Message);

