namespace Adapters.Persistence.Configuration;

/// <summary>
/// Settings for the root admin user.
/// The root admin is created on first startup if no admin users exist.
/// </summary>
public class RootAdminSettings
{
    public const string SectionName = "RootAdmin";

    /// <summary>
    /// Root admin user name (fixed, not configurable for security).
    /// </summary>
    public const string UserName = "root-admin";

    /// <summary>
    /// Root admin email address.
    /// </summary>
    public string Email { get; set; } = "admin@system.local";

    /// <summary>
    /// The initial API key for root admin.
    /// This should be set in environment-specific config and changed after first use.
    /// WARNING: Store securely - treat like a password!
    /// </summary>
    public string InitialApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether to enable automatic root admin creation on startup.
    /// Set to false to disable in production after initial setup.
    /// </summary>
    public bool EnableAutoCreate { get; set; } = true;
}

