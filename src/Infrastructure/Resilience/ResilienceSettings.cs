namespace Infrastructure.Resilience;

/// <summary>
/// Root settings for resilience policies.
/// </summary>
public sealed class ResilienceSettings
{
    public const string SectionName = "Resilience";

    /// <summary>
    /// MongoDB resilience settings.
    /// </summary>
    public ServiceResilienceSettings MongoDB { get; set; } = new();

    /// <summary>
    /// Redis resilience settings.
    /// </summary>
    public ServiceResilienceSettings Redis { get; set; } = new();

    /// <summary>
    /// HTTP client resilience settings for external API calls.
    /// </summary>
    public ServiceResilienceSettings HttpClient { get; set; } = new();
}

/// <summary>
/// Resilience settings for a specific service.
/// </summary>
public sealed class ServiceResilienceSettings
{
    /// <summary>
    /// Retry policy settings.
    /// </summary>
    public RetrySettings Retry { get; set; } = new();

    /// <summary>
    /// Circuit breaker settings.
    /// </summary>
    public CircuitBreakerSettings CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Timeout settings.
    /// </summary>
    public TimeoutSettings Timeout { get; set; } = new();
}

/// <summary>
/// Retry policy settings.
/// </summary>
public sealed class RetrySettings
{
    /// <summary>
    /// Maximum number of retry attempts. Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds between retries. Default: 200ms.
    /// </summary>
    public int BaseDelayMs { get; set; } = 200;

    /// <summary>
    /// Backoff type: Constant, Linear, or Exponential. Default: Exponential.
    /// </summary>
    public string BackoffType { get; set; } = "Exponential";

    /// <summary>
    /// Get the delay backoff type as enum.
    /// </summary>
    public Polly.DelayBackoffType GetBackoffType() => BackoffType?.ToLowerInvariant() switch
    {
        "constant" => Polly.DelayBackoffType.Constant,
        "linear" => Polly.DelayBackoffType.Linear,
        _ => Polly.DelayBackoffType.Exponential
    };
}

/// <summary>
/// Circuit breaker settings.
/// </summary>
public sealed class CircuitBreakerSettings
{
    /// <summary>
    /// Whether circuit breaker is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Failure ratio threshold (0.0 to 1.0). Default: 0.5 (50%).
    /// </summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Sampling duration in seconds. Default: 10.
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 10;

    /// <summary>
    /// Minimum number of calls before circuit breaker can trip. Default: 10.
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Duration the circuit stays open in seconds. Default: 30.
    /// </summary>
    public int BreakDurationSeconds { get; set; } = 30;
}

/// <summary>
/// Timeout settings.
/// </summary>
public sealed class TimeoutSettings
{
    /// <summary>
    /// Whether timeout is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Timeout in seconds. Default: 30.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

