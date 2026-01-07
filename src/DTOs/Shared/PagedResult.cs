using System.Text.Json.Serialization;

namespace DTOs.Shared;

/// <summary>
/// OData-style paged result wrapper.
/// Follows OData specification for collection responses.
/// https://docs.oasis-open.org/odata/odata-json-format/v4.01/odata-json-format-v4.01.html
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// Total count of items (before pagination).
    /// Only included when $count=true is requested.
    /// OData property: @odata.count
    /// </summary>
    [JsonPropertyName("@odata.count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ODataCount { get; init; }

    /// <summary>
    /// Link to the next page of results.
    /// Null when there are no more pages.
    /// OData property: @odata.nextLink
    /// </summary>
    [JsonPropertyName("@odata.nextLink")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ODataNextLink { get; init; }

    /// <summary>
    /// The collection of items for this page.
    /// OData property: value
    /// </summary>
    [JsonPropertyName("value")]
    public required IReadOnlyList<T> Value { get; init; }

    /// <summary>
    /// Creates a paged result with the given items.
    /// </summary>
    public static PagedResult<T> Create(
        IReadOnlyList<T> items,
        long? totalCount = null,
        string? nextLink = null)
    {
        return new PagedResult<T>
        {
            Value = items,
            ODataCount = totalCount,
            ODataNextLink = nextLink
        };
    }
}

/// <summary>
/// Internal paged data structure returned from repositories.
/// Contains items and total count for building PagedResult responses.
/// </summary>
/// <typeparam name="T">The type of items.</typeparam>
public sealed record PagedData<T>(
    IReadOnlyList<T> Items,
    long TotalCount);

