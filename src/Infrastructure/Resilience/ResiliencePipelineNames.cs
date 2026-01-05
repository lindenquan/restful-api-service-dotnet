namespace Infrastructure.Resilience;

/// <summary>
/// Constants for resilience pipeline names.
/// </summary>
public static class ResiliencePipelineNames
{
    /// <summary>
    /// Pipeline for MongoDB operations.
    /// </summary>
    public const string MongoDB = "mongodb";

    /// <summary>
    /// Pipeline for Redis operations.
    /// </summary>
    public const string Redis = "redis";

    /// <summary>
    /// Pipeline for external HTTP client calls.
    /// </summary>
    public const string HttpClient = "http-client";
}

