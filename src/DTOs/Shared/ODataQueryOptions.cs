namespace DTOs.Shared;

/// <summary>
/// OData query parameters for pagination, sorting, and counting.
/// Supports: $top, $skip, $count, $orderby
/// Does NOT support: $filter, $expand, $select (by design for security and Clean Architecture)
/// </summary>
public sealed class ODataQueryOptions
{
    /// <summary>
    /// Maximum number of items to return (OData $top parameter).
    /// </summary>
    public int? Top { get; init; }

    /// <summary>
    /// Number of items to skip (OData $skip parameter).
    /// </summary>
    public int? Skip { get; init; }

    /// <summary>
    /// Whether to include total count in response (OData $count parameter).
    /// </summary>
    public bool? Count { get; init; }

    /// <summary>
    /// Sort expression (OData $orderby parameter).
    /// Examples: "orderDate desc", "lastName,firstName", "orderDate desc,patientName asc"
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// Gets the effective skip value (defaults to 0).
    /// </summary>
    public int EffectiveSkip => Skip ?? 0;

    /// <summary>
    /// Gets the effective top value with settings applied.
    /// </summary>
    public int GetEffectiveTop(PaginationSettings settings)
    {
        var requestedTop = Top ?? settings.DefaultPageSize;
        return Math.Min(requestedTop, settings.MaxPageSize);
    }

    /// <summary>
    /// Gets the effective count value with settings applied.
    /// </summary>
    public bool GetEffectiveCount(PaginationSettings settings)
    {
        return Count ?? settings.IncludeCountByDefault;
    }

    /// <summary>
    /// Parses the $orderby parameter into individual sort clauses.
    /// Returns list of (field, descending) tuples.
    /// </summary>
    public List<(string Field, bool Descending)> ParseOrderBy()
    {
        if (string.IsNullOrWhiteSpace(OrderBy))
            return new List<(string, bool)>();

        var result = new List<(string Field, bool Descending)>();
        var clauses = OrderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var clause in clauses)
        {
            var parts = clause.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var field = parts[0];
            var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
            result.Add((field, descending));
        }

        return result;
    }

    /// <summary>
    /// Validates that all sort fields are in the allowed whitelist.
    /// Returns true if valid, false if any field is not allowed.
    /// </summary>
    public bool ValidateSortFields(HashSet<string> allowedFields)
    {
        var sortClauses = ParseOrderBy();
        return sortClauses.All(clause => allowedFields.Contains(clause.Field));
    }

    /// <summary>
    /// Gets the first sort field and direction (for simple sorting).
    /// Returns null if no $orderby specified.
    /// </summary>
    public (string Field, bool Descending)? GetPrimarySortField()
    {
        var sortClauses = ParseOrderBy();
        return sortClauses.FirstOrDefault();
    }
}

