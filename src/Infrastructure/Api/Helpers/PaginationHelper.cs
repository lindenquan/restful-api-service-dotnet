using DTOs.Shared;

namespace Infrastructure.Api.Helpers;

/// <summary>
/// Helper methods for building OData-compliant paginated responses.
/// Supports: $top, $skip, $count, $orderby
/// Response format: @odata.context, @odata.count, @odata.nextLink, value
/// </summary>
public static class PaginationHelper
{
    /// <summary>
    /// Builds a PagedResult from paged data and OData query options.
    /// </summary>
    /// <typeparam name="TEntity">Domain entity type.</typeparam>
    /// <typeparam name="TDto">DTO type.</typeparam>
    /// <param name="data">Paged data from repository.</param>
    /// <param name="mapper">Function to map entities to DTOs.</param>
    /// <param name="request">HTTP request for building context and next link.</param>
    /// <param name="query">OData query parameters.</param>
    /// <param name="settings">Pagination settings.</param>
    /// <param name="entitySetName">Entity set name for @odata.context (e.g., "Orders", "Patients").</param>
    /// <returns>PagedResult with full OData properties.</returns>
    public static PagedResult<TDto> BuildPagedResult<TEntity, TDto>(
        PagedData<TEntity> data,
        Func<TEntity, TDto> mapper,
        HttpRequest request,
        ODataQueryOptions query,
        PaginationSettings settings,
        string entitySetName)
    {
        var dtos = data.Items.Select(mapper).ToList();
        var effectiveTop = query.GetEffectiveTop(settings);
        var effectiveSkip = query.EffectiveSkip;
        var includeCount = query.GetEffectiveCount(settings);

        // Build @odata.context
        var context = BuildODataContext(request, entitySetName);

        // Calculate next link if there are more items
        string? nextLink = null;
        if (includeCount && data.TotalCount > effectiveSkip + dtos.Count)
        {
            nextLink = BuildNextLink(request, effectiveSkip + effectiveTop, effectiveTop, query);
        }
        else if (!includeCount && dtos.Count == effectiveTop)
        {
            // If we got a full page and don't know total, assume there might be more
            nextLink = BuildNextLink(request, effectiveSkip + effectiveTop, effectiveTop, query);
        }

        return PagedResult<TDto>.Create(
            items: dtos,
            context: context,
            totalCount: includeCount ? data.TotalCount : null,
            nextLink: nextLink);
    }

    /// <summary>
    /// Builds @odata.context URL.
    /// Format: {scheme}://{host}/odata/$metadata#{entitySetName}
    /// </summary>
    private static string BuildODataContext(HttpRequest request, string entitySetName)
    {
        var scheme = request.Scheme;
        var host = request.Host.ToString();
        return $"{scheme}://{host}/odata/$metadata#{entitySetName}";
    }

    /// <summary>
    /// Builds @odata.nextLink URL with OData query parameters.
    /// </summary>
    private static string BuildNextLink(
        HttpRequest request,
        int nextSkip,
        int top,
        ODataQueryOptions query)
    {
        var scheme = request.Scheme;
        var host = request.Host.ToString();
        var path = request.Path.ToString();

        var queryParams = new List<string>
        {
            $"$skip={nextSkip}",
            $"$top={top}"
        };

        if (query.Count == true)
        {
            queryParams.Add("$count=true");
        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            queryParams.Add($"$orderby={Uri.EscapeDataString(query.OrderBy)}");
        }

        return $"{scheme}://{host}{path}?{string.Join("&", queryParams)}";
    }
}

