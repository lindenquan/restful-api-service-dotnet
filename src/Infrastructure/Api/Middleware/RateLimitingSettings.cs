namespace Infrastructure.Api.Middleware;

/// <summary>
/// Configuration settings for adaptive rate limiting.
/// When system resources exceed thresholds, requests are rejected with 429.
/// </summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Whether rate limiting is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Memory usage threshold (0-100). When GC memory load exceeds this percentage
    /// of the available heap, new requests are rejected. Default: 85%.
    ///
    /// Note: This is relative to GC's TotalAvailableMemoryBytes, which respects
    /// DOTNET_GCHeapHardLimitPercent. For example, with a 1GB container and
    /// GCHeapHardLimitPercent=75, the heap limit is 750MB. Setting this to 85%
    /// means rate limiting triggers at 637.5MB (85% of 750MB = 63.75% of container).
    /// This provides headroom before hitting the hard limit.
    /// </summary>
    public int MemoryThresholdPercent { get; set; } = 85;

    /// <summary>
    /// Thread pool utilization threshold (0-100). When worker or IO threads
    /// exceed this percentage, new requests are rejected. Default: 90%.
    /// </summary>
    public int ThreadPoolThresholdPercent { get; set; } = 90;

    /// <summary>
    /// Maximum pending work items in thread pool queue.
    /// When exceeded, new requests are rejected. Default: 1000.
    /// </summary>
    public int PendingWorkItemsThreshold { get; set; } = 1000;

    /// <summary>
    /// How often to check system metrics (milliseconds).
    /// Lower values are more responsive but add overhead. Default: 100ms.
    /// </summary>
    public int CheckIntervalMs { get; set; } = 100;

    /// <summary>
    /// Retry-After header value in seconds. Default: 10.
    /// </summary>
    public int RetryAfterSeconds { get; set; } = 10;
}

/// <summary>
/// Extension methods for rate limiting middleware.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds adaptive rate limiting middleware to the pipeline.
    /// Should be added early in the pipeline (after exception handling).
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
        => builder.UseMiddleware<RateLimitingMiddleware>();
}

