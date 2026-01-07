# Pagination

This API implements **OData-style pagination** following the [OData 4.0 specification](https://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part1-protocol.html) for query parameters and response format.

## Why OData?

OData is a widely adopted REST API standard that provides:
- **Interoperability**: Works with Microsoft Power Platform, Excel, SharePoint, and many OData-aware tools
- **Consistency**: Well-documented query parameter conventions (`$top`, `$skip`, `$orderby`)
- **Client libraries**: Available for .NET, JavaScript, Python, Java, and more
- **Discoverability**: Standard response format with `@odata.nextLink` for cursor-based navigation

## Query Parameters

All collection endpoints support these OData query parameters:

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `$top` | integer | Number of items to return (max: 100) | `$top=20` |
| `$skip` | integer | Number of items to skip | `$skip=40` |
| `$count` | boolean | Include total count in response | `$count=true` |
| `$orderby` | string | Sort field and direction | `$orderby=lastName desc` |

### Examples

```http
# Get first 10 patients, sorted by last name
GET /api/v2/patients?$top=10&$orderby=lastName

# Get page 3 (items 20-29) with total count
GET /api/v2/patients?$top=10&$skip=20&$count=true

# Sort by creation date descending
GET /api/v2/prescriptions?$orderby=createdAt desc
```

## Response Format

Paginated responses follow OData conventions:

```json
{
  "@odata.count": 150,
  "@odata.nextLink": "https://api.example.com/api/v2/patients?$top=20&$skip=20&$count=true",
  "value": [
    { "id": "...", "firstName": "John", "lastName": "Doe" },
    { "id": "...", "firstName": "Jane", "lastName": "Smith" }
  ]
}
```

### Response Properties

| Property | Description | Presence |
|----------|-------------|----------|
| `value` | Array of items | Always |
| `@odata.count` | Total item count | Only when `$count=true` |
| `@odata.nextLink` | URL for next page | Only when more items exist |

## Configuration

Configure pagination defaults in `appsettings.json`:

```json
{
  "Pagination": {
    "DefaultPageSize": 20,
    "MaxPageSize": 100,
    "DefaultIncludeCount": false
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `DefaultPageSize` | Items returned when `$top` not specified | 20 |
| `MaxPageSize` | Maximum allowed `$top` value (larger values are clamped) | 100 |
| `DefaultIncludeCount` | Include `@odata.count` when `$count` not specified | false |

> **Performance Note**: The `$count` operation can be expensive on large collections as it requires a full count query. Consider leaving `DefaultIncludeCount` as `false` and having clients explicitly request counts when needed.

## Sorting

The `$orderby` parameter supports sorting by a single field:

```http
# Ascending (default)
GET /api/v2/patients?$orderby=lastName

# Descending
GET /api/v2/patients?$orderby=createdAt desc
```

Sortable fields depend on the resource. Invalid field names are ignored and default sorting is applied.

## Implementation Details

### Controller Layer

Controllers accept `ODataQueryParams` and return `PagedResult<T>`:

```csharp
[HttpGet]
public async Task<ActionResult<PagedResult<PatientDto>>> GetAll(
    [FromQuery] ODataQueryParams query,
    CancellationToken ct)
{
    var pagedData = await _mediator.Send(new GetPatientsPagedQuery(
        query.EffectiveSkip,
        query.GetEffectiveTop(_paginationSettings),
        query.GetEffectiveCount(_paginationSettings),
        orderByProperty,
        descending), ct);
    
    return Ok(PaginationHelper.BuildPagedResult(pagedData, ...));
}
```

### Repository Layer

Repositories implement efficient skip/take pagination:

```csharp
public async Task<PagedData<Patient>> GetPagedAsync(
    int skip, int take, bool includeCount,
    string? orderBy, bool descending, CancellationToken ct)
{
    var query = _collection.Find(FilterDefinition<Patient>.Empty);
    
    // Apply sorting
    if (!string.IsNullOrEmpty(orderBy))
        query = descending 
            ? query.SortByDescending(GetSortField(orderBy))
            : query.SortBy(GetSortField(orderBy));
    
    // Apply pagination
    var items = await query.Skip(skip).Limit(take).ToListAsync(ct);
    
    // Get count if requested
    long? count = includeCount 
        ? await _collection.CountDocumentsAsync(FilterDefinition<Patient>.Empty, ct)
        : null;
    
    return new PagedData<Patient>(items, count);
}
```

## Client Usage

### C# with HttpClient

```csharp
var response = await client.GetFromJsonAsync<ODataResponse<Patient>>(
    "/api/v2/patients?$top=10&$count=true");

foreach (var patient in response.Value)
    Console.WriteLine(patient.FullName);

if (response.NextLink != null)
    // Fetch next page...
```

### JavaScript/TypeScript

```typescript
const response = await fetch('/api/v2/patients?$top=10&$count=true');
const data = await response.json();

console.log(`Total: ${data['@odata.count']}`);
data.value.forEach(p => console.log(p.fullName));

if (data['@odata.nextLink']) {
  // Fetch next page...
}
```

## Best Practices

1. **Always limit page size**: Use `$top` to avoid fetching entire collections
2. **Use `$count` sparingly**: Only request counts when needed for UI pagination
3. **Prefer cursor-based navigation**: Use `@odata.nextLink` rather than calculating `$skip`
4. **Index sort fields**: Ensure database indexes exist for commonly sorted fields

