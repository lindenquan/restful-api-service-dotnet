using System.Security.Claims;
using Entities;

namespace Adapters.Api.Services;

/// <summary>
/// Interface for accessing current authenticated user information.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Current user's ID.
    /// </summary>
    int? UserId { get; }

    /// <summary>
    /// Current user's name.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Current user's email.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Current user's type (Regular or Admin).
    /// </summary>
    UserType UserType { get; }

    /// <summary>
    /// Whether the current user is an admin.
    /// </summary>
    bool IsAdmin { get; }

    /// <summary>
    /// Whether there is an authenticated user.
    /// </summary>
    bool IsAuthenticated { get; }
}

/// <summary>
/// Implementation that reads user info from HttpContext claims.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public string? UserName => User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public UserType UserType
    {
        get
        {
            var userTypeClaim = User?.FindFirst("UserType")?.Value;
            return Enum.TryParse<UserType>(userTypeClaim, out var userType)
                ? userType
                : UserType.Regular;
        }
    }

    public bool IsAdmin => UserType == UserType.Admin;
}

