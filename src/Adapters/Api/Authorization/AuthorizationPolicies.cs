using Entities;

namespace Adapters.Api.Authorization;

/// <summary>
/// Authorization policy names.
/// </summary>
public static class PolicyNames
{
    /// <summary>
    /// Policy for read operations - both Regular and Admin users can access.
    /// </summary>
    public const string CanRead = "CanRead";

    /// <summary>
    /// Policy for create operations - both Regular and Admin users can access.
    /// </summary>
    public const string CanCreate = "CanCreate";

    /// <summary>
    /// Policy for update operations - both Regular and Admin users can access.
    /// </summary>
    public const string CanUpdate = "CanUpdate";

    /// <summary>
    /// Policy for delete operations - both can soft delete, but behavior differs.
    /// </summary>
    public const string CanDelete = "CanDelete";

    /// <summary>
    /// Policy for hard delete - Admin users only.
    /// </summary>
    public const string CanHardDelete = "CanHardDelete";

    /// <summary>
    /// Admin only policy.
    /// </summary>
    public const string AdminOnly = "AdminOnly";
}

/// <summary>
/// Extension methods for configuring authorization policies.
/// </summary>
public static class AuthorizationPoliciesExtensions
{
    public static IServiceCollection AddApiKeyAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Read policy - all authenticated users
            options.AddPolicy(PolicyNames.CanRead, policy =>
                policy.RequireAuthenticatedUser());

            // Create policy - all authenticated users
            options.AddPolicy(PolicyNames.CanCreate, policy =>
                policy.RequireAuthenticatedUser());

            // Update policy - all authenticated users
            options.AddPolicy(PolicyNames.CanUpdate, policy =>
                policy.RequireAuthenticatedUser());

            // Delete policy - all authenticated users (soft delete for Regular, option for hard delete for Admin)
            options.AddPolicy(PolicyNames.CanDelete, policy =>
                policy.RequireAuthenticatedUser());

            // Hard Delete policy - Admin only
            options.AddPolicy(PolicyNames.CanHardDelete, policy =>
                policy.RequireRole(UserType.Admin.ToString()));

            // Admin only policy
            options.AddPolicy(PolicyNames.AdminOnly, policy =>
                policy.RequireRole(UserType.Admin.ToString()));
        });

        return services;
    }
}

