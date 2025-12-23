namespace Adapters.Persistence.Configuration;

/// <summary>
/// Rate limiting configuration settings.
/// </summary>
public class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum concurrent requests allowed.
    /// When exceeded, new requests get 429 response.
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Maximum requests that can wait in queue.
    /// </summary>
    public int QueueLimit { get; set; } = 50;

    /// <summary>
    /// Message returned when rate limit is exceeded.
    /// </summary>
    public string RejectionMessage { get; set; } = "Too many requests. Please try again later.";
}

