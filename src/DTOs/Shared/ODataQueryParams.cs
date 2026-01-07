using Microsoft.AspNetCore.Mvc;

namespace DTOs.Shared;

/// <summary>
/// OData-style query parameters for pagination.
/// Binds from query string parameters: $top, $skip, $count, $orderby
/// 
/// Example: /api/v1/orders?$top=20&amp;$skip=40&amp;$count=true&amp;$orderby=orderDate desc
/// </summary>
public sealed class ODataQueryParams
{
    /// <summary>
    /// Number of items to return. Equivalent to "page size".
    /// If not specified, uses PaginationSettings.DefaultPageSize.
    /// Clamped to PaginationSettings.MaxPageSize.
    /// </summary>
    [FromQuery(Name = "$top")]
    public int? Top { get; set; }

    /// <summary>
    /// Number of items to skip. Equivalent to "offset".
    /// Default: 0 (start from beginning).
    /// 
    /// To get page N (0-indexed): $skip = N * $top
    /// Example: Page 3 with size 20 = $skip=60&amp;$top=20
    /// </summary>
    [FromQuery(Name = "$skip")]
    public int? Skip { get; set; }

    /// <summary>
    /// Whether to include @odata.count in the response.
    /// When true, returns total count of items (before pagination).
    /// Note: Counting can be expensive on large collections.
    /// </summary>
    [FromQuery(Name = "$count")]
    public bool? Count { get; set; }

    /// <summary>
    /// Order by expression.
    /// Format: "propertyName [asc|desc]"
    /// Example: "orderDate desc" or "lastName asc"
    /// 
    /// Note: Not all properties may be sortable. Invalid properties are ignored.
    /// </summary>
    [FromQuery(Name = "$orderby")]
    public string? OrderBy { get; set; }

    /// <summary>
    /// Gets the effective skip value (defaults to 0).
    /// </summary>
    public int EffectiveSkip => Math.Max(0, Skip ?? 0);

    /// <summary>
    /// Gets the effective top value, applying defaults and limits.
    /// </summary>
    /// <param name="settings">Pagination settings for defaults and limits.</param>
    public int GetEffectiveTop(PaginationSettings settings)
    {
        var top = Top ?? settings.DefaultPageSize;
        return Math.Clamp(top, 1, settings.MaxPageSize);
    }

    /// <summary>
    /// Gets whether to include count in response.
    /// </summary>
    /// <param name="settings">Pagination settings for default.</param>
    public bool GetEffectiveCount(PaginationSettings settings)
    {
        return Count ?? settings.DefaultIncludeCount;
    }

    /// <summary>
    /// Parses the $orderby expression.
    /// </summary>
    /// <returns>Tuple of (propertyName, isDescending) or null if not specified/invalid.</returns>
    public (string Property, bool Descending)? ParseOrderBy()
    {
        if (string.IsNullOrWhiteSpace(OrderBy))
            return null;

        var parts = OrderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        var property = parts[0];
        var descending = parts.Length > 1 &&
            parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (property, descending);
    }
}

