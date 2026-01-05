namespace Infrastructure.Persistence.Configuration;

/// <summary>
/// CORS configuration settings.
/// </summary>
public class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Allowed origins for CORS requests.
    /// Use "*" for development, specific origins for production.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Allowed HTTP methods.
    /// </summary>
    public string[] AllowedMethods { get; set; } = ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"];

    /// <summary>
    /// Allowed request headers.
    /// </summary>
    public string[] AllowedHeaders { get; set; } = ["Content-Type", "Authorization", "X-Api-Key", "X-Requested-With"];

    /// <summary>
    /// Headers exposed to the browser.
    /// </summary>
    public string[] ExposedHeaders { get; set; } = ["X-Pagination", "X-Request-Id"];

    /// <summary>
    /// Whether to allow credentials (cookies, authorization headers).
    /// Cannot be true if AllowedOrigins contains "*".
    /// </summary>
    public bool AllowCredentials { get; set; } = false;

    /// <summary>
    /// Preflight cache duration in seconds.
    /// </summary>
    public int PreflightMaxAgeSeconds { get; set; } = 600;
}

