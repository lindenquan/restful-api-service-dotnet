namespace Entities;

/// <summary>
/// User type for authorization.
/// </summary>
public enum UserType
{
    /// <summary>
    /// Regular users can only Create and Read.
    /// Delete operation performs soft delete.
    /// </summary>
    Regular = 0,

    /// <summary>
    /// Admin users can perform all operations.
    /// Delete operation can permanently remove documents.
    /// </summary>
    Admin = 1
}

/// <summary>
/// User entity for API authentication and authorization.
/// Stores API key credentials for clients accessing the API.
/// Note: The actual API key is hashed using SHA-256 before storage.
/// </summary>
public class User : BaseEntity
{
    /// <summary>
    /// SHA-256 hash of the API key (hex-encoded, lowercase).
    /// The original key cannot be retrieved - only validated.
    /// </summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>
    /// First 8 characters of the API key for identification/logging.
    /// Example: "abc12345..."
    /// </summary>
    public string ApiKeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// User's display name.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User type (Regular or Admin).
    /// Determines what operations the user can perform.
    /// </summary>
    public UserType UserType { get; set; } = UserType.Regular;

    /// <summary>
    /// Whether the API key is active.
    /// Inactive keys are rejected during authentication.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional description for the API key.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Last time this API key was used.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
