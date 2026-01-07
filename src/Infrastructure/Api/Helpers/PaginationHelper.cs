using DTOs.Shared;

namespace Infrastructure.Api.Helpers;

/// <summary>
/// Helper methods for building OData-style paginated responses.
/// </summary>
public static class PaginationHelper
{
    /// <summary>
    /// Builds a PagedResult from paged data and request context.
    /// </summary>
    /// <typeparam name="TEntity">Domain entity type.</typeparam>
    /// <typeparam name="TDto">DTO type.</typeparam>
    /// <param name="data">Paged data from repository.</param>
    /// <param name="mapper">Function to map entities to DTOs.</param>
    /// <param name="request">HTTP request for building next link.</param>
    /// <param name="query">OData query parameters.</param>
    /// <param name="settings">Pagination settings.</param>
    /// <returns>PagedResult with OData properties.</returns>
    public static PagedResult<TDto> BuildPagedResult<TEntity, TDto>(
        PagedData<TEntity> data,
        Func<TEntity, TDto> mapper,
        HttpRequest request,
        ODataQueryParams query,
        PaginationSettings settings)
    {
        var dtos = data.Items.Select(mapper).ToList();
        var effectiveTop = query.GetEffectiveTop(settings);
        var effectiveSkip = query.EffectiveSkip;
        var includeCount = query.GetEffectiveCount(settings);

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
            dtos,
            includeCount ? data.TotalCount : null,
            nextLink);
    }

    /// <summary>
    /// Builds a PagedResult directly from DTOs (when mapping is done elsewhere).
    /// </summary>
    public static PagedResult<TDto> BuildPagedResult<TDto>(
        IReadOnlyList<TDto> items,
        long totalCount,
        HttpRequest request,
        ODataQueryParams query,
        PaginationSettings settings)
    {
        var effectiveTop = query.GetEffectiveTop(settings);
        var effectiveSkip = query.EffectiveSkip;
        var includeCount = query.GetEffectiveCount(settings);

        // Calculate next link if there are more items
        string? nextLink = null;
        if (includeCount && totalCount > effectiveSkip + items.Count)
        {
            nextLink = BuildNextLink(request, effectiveSkip + effectiveTop, effectiveTop, query);
        }
        else if (!includeCount && items.Count == effectiveTop)
        {
            nextLink = BuildNextLink(request, effectiveSkip + effectiveTop, effectiveTop, query);
        }

        return PagedResult<TDto>.Create(
            items,
            includeCount ? totalCount : null,
            nextLink);
    }

    private static string BuildNextLink(
        HttpRequest request,
        int nextSkip,
        int top,
        ODataQueryParams query)
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

