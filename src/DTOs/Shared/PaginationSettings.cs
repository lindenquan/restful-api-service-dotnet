namespace DTOs.Shared;

/// <summary>
/// Configuration settings for OData-style pagination.
/// Shared across all API versions.
/// </summary>
public sealed class PaginationSettings
{
    public const string SectionName = "Pagination";

    /// <summary>
    /// Default page size when $top is not specified. Default: 20.
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Maximum allowed page size to prevent excessive data retrieval. Default: 100.
    /// Requests with $top > MaxPageSize will be clamped to MaxPageSize.
    /// </summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// Whether to include @odata.count by default. Default: false.
    /// Can be overridden per-request with $count=true query parameter.
    /// Note: Counting can be expensive on large collections.
    /// </summary>
    public bool DefaultIncludeCount { get; set; } = false;
}

