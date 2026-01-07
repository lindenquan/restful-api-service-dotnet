namespace Infrastructure.Api.Middleware;

/// <summary>
/// Configuration for request timeout middleware.
///
/// IMPORTANT: Kestrel has NO default request processing timeout. This is a significant
/// gap compared to other servers like Tomcat (20s), Nginx (60s), or IIS (110s).
/// Without this middleware, a single infinite loop or deadlock can hold a connection forever.
///
/// This middleware provides two levels of timeout protection:
/// 1. Processing timeout (DefaultTimeoutSeconds) - covers controller execution, DB queries, serialization
/// 2. Total timeout (TotalTimeoutSeconds) - covers ENTIRE request including response download by client
/// </summary>
public sealed class RequestTimeoutSettings
{
    /// <summary>
    /// Enable or disable request timeout middleware. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default timeout for request PROCESSING in seconds. Default: 60 seconds.
    /// This applies to:
    /// - Controller execution
    /// - Database queries
    /// - External HTTP calls
    /// - Response serialization
    ///
    /// Does NOT include time for client to download response.
    /// Triggers OperationCanceledException, returns 408 if response not started.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Total timeout for ENTIRE request including response download. Default: 120 seconds.
    /// This is a hard limit that aborts the connection if exceeded.
    ///
    /// Set to 0 to disable (rely only on processing timeout + MinResponseDataRate).
    ///
    /// Should be greater than DefaultTimeoutSeconds.
    /// Should be less than Kubernetes terminationGracePeriodSeconds (60s) to ensure
    /// connections complete during graceful shutdown.
    ///
    /// Example: With 50s total timeout, even a slow client downloading response
    /// will be disconnected after 50s, allowing graceful shutdown to complete.
    /// </summary>
    public int TotalTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Endpoint-specific timeout overrides for processing timeout.
    /// Key: Path prefix (e.g., "/api/v1/reports")
    /// Value: Timeout in seconds
    ///
    /// Example: { "/api/v1/reports": 300 } gives reports 5 minutes instead of default.
    /// Note: TotalTimeoutSeconds still applies as the hard limit.
    /// </summary>
    public Dictionary<string, int> EndpointTimeouts { get; set; } = new();
}

