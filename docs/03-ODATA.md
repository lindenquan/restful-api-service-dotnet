# OData Implementation Guide

> **⚡ Quick Start**: Jump to [Quick Reference](#quick-reference) for common usage examples

## Table of Contents
- [Quick Reference](#quick-reference) - Common usage examples
- [Overview](#overview) - Why OData and our approach
- [What We Implement](#what-we-implement-vs-dont-implement) - Feature comparison table
- [Architecture](#architecture) - How it works with Clean Architecture
- [Usage Examples](#usage-examples) - Detailed examples
- [Implementation Details](#implementation-details) - Code walkthrough
- [Configuration](#configuration) - Settings and options
- [Testing](#testing) - How to test OData endpoints
- [Best Practices](#best-practices) - Performance and security tips

---

## Quick Reference

### OData Query Parameters

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `$top` | integer | Maximum items to return | `$top=20` |
| `$skip` | integer | Items to skip | `$skip=40` |
| `$count` | boolean | Include total count | `$count=true` |
| `$orderby` | string | Sort expression | `$orderby=orderDate desc` |

### Common Examples

**Pagination:**
```http
GET /api/v2/orders?$top=20&$skip=0   # First page
GET /api/v2/orders?$top=20&$skip=20  # Second page
```

**Sorting:**
```http
GET /api/v2/orders?$orderby=orderDate desc
GET /api/v2/patients?$orderby=lastName,firstName
```

**Combined:**
```http
GET /api/v2/orders?$top=50&$skip=100&$count=true&$orderby=orderDate desc
```

**Response Format:**
```json
{
  "@odata.context": "https://api.example.com/odata/$metadata#Orders",
  "@odata.count": 150,
  "@odata.nextLink": "https://api.example.com/api/v2/orders?$skip=60&$top=20",
  "value": [...]
}
```

---

## Overview

This API implements a **safe subset of OData v4** for querying and pagination. We use OData query syntax and response format WITHOUT the `[EnableQuery]` attribute to maintain Clean Architecture principles and MongoDB compatibility.

**Why OData?**
- ✅ Industry standard (OASIS specification)
- ✅ Excellent tool support (Power BI, Excel, Postman)
- ✅ Well-documented and widely understood
- ✅ Works perfectly with MongoDB for simple queries
- ✅ Maintains Clean Architecture (no direct database access)

**Why NOT full OData?**
- ❌ `[EnableQuery]` bypasses business logic and validation
- ❌ Complex queries (`$expand`, `$filter` with lambdas) don't work well with MongoDB
- ❌ Security risks from arbitrary queries
- ❌ Performance unpredictability

---

## What We Implement vs Don't Implement

### ✅ Implemented OData Features (Safe Subset)

| Feature | Status | MongoDB Support | Use Case | Example |
|---------|--------|-----------------|----------|---------|
| **$top** | ✅ Implemented | Perfect | Page size | `?$top=20` |
| **$skip** | ✅ Implemented | Perfect | Pagination offset | `?$skip=40` |
| **$count** | ✅ Implemented | Perfect | Include total count | `?$count=true` |
| **$orderby** (single field) | ✅ Implemented | Perfect | Sort by one field | `?$orderby=orderDate desc` |
| **$orderby** (multi-field) | ✅ Implemented | Perfect | Sort by multiple fields | `?$orderby=lastName,firstName` |
| **@odata.context** | ✅ Implemented | N/A | Metadata URL | Response property |
| **@odata.count** | ✅ Implemented | N/A | Total item count | Response property |
| **@odata.nextLink** | ✅ Implemented | N/A | Next page URL | Response property |
| **$metadata** endpoint | ✅ Implemented | N/A | Schema discovery | `/odata/$metadata` |

### ❌ NOT Implemented OData Features (Reasons)

| Feature | Status | Reason | Alternative |
|---------|--------|--------|-------------|
| **$filter** | ❌ Not Implemented | Security risk, complex MongoDB translation, unpredictable performance | Use explicit query parameters (e.g., `?status=pending&orderDateFrom=2024-01-01`) |
| **$expand** | ❌ Not Implemented | MongoDB has no SQL-style joins, causes N+1 queries, performance nightmare | Include related data in DTOs at repository layer |
| **$select** | ❌ Not Implemented | Exposes schema, breaks API versioning, complex with DTOs | Use versioned DTOs (V1, V2) or create specific endpoints |
| **$search** | ❌ Not Implemented | Requires MongoDB text indexes, limited functionality | Implement dedicated search endpoints if needed |
| **[EnableQuery]** | ❌ Not Implemented | Bypasses Clean Architecture, business logic, validation, caching | Manual parsing with MediatR |
| **Lambda expressions** (`any()`, `all()`) | ❌ Not Implemented | Poor MongoDB support, complex translation | Use explicit query parameters |
| **String functions** (`contains()`, `tolower()`) | ❌ Not Implemented | Limited MongoDB support | Implement in repository layer |
| **Date functions** (`year()`, `month()`) | ❌ Not Implemented | Limited MongoDB support | Implement in repository layer |

---

## Architecture

### Request Flow

```
HTTP Request with OData params
    ↓
Controller (parses $top, $skip, $count, $orderby)
    ↓
MediatR Query (simple parameters: skip, top, orderBy)
    ↓
Handler (business logic, validation, authorization)
    ↓
Repository (MongoDB queries with Skip, Limit, SortBy)
    ↓
Domain Entities
    ↓
Controller (maps to DTOs)
    ↓
OData Response (@odata.context, @odata.count, @odata.nextLink, value)
```

### Clean Architecture Maintained

- ✅ **Controllers**: Parse OData parameters, validate, map to DTOs
- ✅ **MediatR**: Business logic, validation, authorization, caching
- ✅ **Repositories**: Simple MongoDB queries (Skip, Limit, SortBy)
- ✅ **No direct database access**: All queries go through business logic

---

## Usage Examples

### Basic Pagination

```http
GET /api/v2/orders?$top=20&$skip=40
```

**Response:**
```json
{
  "@odata.context": "https://api.example.com/odata/$metadata#Orders",
  "@odata.count": 150,
  "@odata.nextLink": "https://api.example.com/api/v2/orders?$skip=60&$top=20",
  "value": [
    {
      "id": "01936f3e-8b2a-7890-b456-123456789abc",
      "patientName": "John Doe",
      "medicationName": "Aspirin 100mg",
      "orderDate": "2024-01-07T10:30:00Z",
      "status": "pending"
    }
  ]
}
```

### Sorting (Single Field)

```http
GET /api/v2/orders?$orderby=orderDate desc
```

### Sorting (Multiple Fields)

```http
GET /api/v2/orders?$orderby=lastName,firstName
GET /api/v2/orders?$orderby=orderDate desc,patientName asc
```

### Disable Count (Performance Optimization)

```http
GET /api/v2/orders?$top=20&$count=false
```

**Response:**
```json
{
  "@odata.context": "https://api.example.com/odata/$metadata#Orders",
  "@odata.nextLink": "https://api.example.com/api/v2/orders?$skip=20&$top=20",
  "value": [...]
}
```

### Combined Query

```http
GET /api/v2/orders?$top=50&$skip=100&$count=true&$orderby=orderDate desc
```

---

## Implementation Details

### 1. ODataQueryOptions Helper Class

Location: `src/DTOs/Shared/ODataQueryOptions.cs`

```csharp
public sealed class ODataQueryOptions
{
    public int? Top { get; init; }
    public int? Skip { get; init; }
    public bool? Count { get; init; }
    public string? OrderBy { get; init; }
    
    public int EffectiveSkip => Skip ?? 0;
    
    public int GetEffectiveTop(PaginationSettings settings)
    {
        var requestedTop = Top ?? settings.DefaultPageSize;
        return Math.Min(requestedTop, settings.MaxPageSize);
    }
    
    public bool GetEffectiveCount(PaginationSettings settings)
    {
        return Count ?? settings.IncludeCountByDefault;
    }
    
    public List<(string Field, bool Descending)> ParseOrderBy() { ... }
    public bool ValidateSortFields(HashSet<string> allowedFields) { ... }
    public (string Field, bool Descending)? GetPrimarySortField() { ... }
}
```

### 2. Controller Implementation

```csharp
[HttpGet]
[Authorize(Policy = PolicyNames.CanRead)]
public async Task<ActionResult<PagedResult<PrescriptionOrderDto>>> GetAll(
    [FromQuery] ODataQueryOptions query,
    CancellationToken ct)
{
    // Parse primary sort field
    var primarySort = query.GetPrimarySortField();

    // Send to MediatR (business logic, validation, caching)
    var pagedData = await _mediator.Send(new GetOrdersPagedQuery(
        query.EffectiveSkip,
        query.GetEffectiveTop(_paginationSettings),
        query.GetEffectiveCount(_paginationSettings),
        primarySort?.Field,
        primarySort?.Descending ?? false), ct);

    // Build OData response
    var result = PaginationHelper.BuildPagedResult(
        pagedData,
        PrescriptionOrderMapper.ToV2Dto,
        Request,
        query,
        _paginationSettings,
        "Orders");  // Entity set name for @odata.context

    return Ok(result);
}
```

### 3. MediatR Query

```csharp
public sealed record GetOrdersPagedQuery(
    int Skip,
    int Top,
    bool IncludeCount = false,
    string? OrderBy = null,
    bool Descending = false) : IRequest<PagedData<PrescriptionOrder>>;
```

### 4. Repository Implementation (MongoDB)

```csharp
public async Task<PagedData<PrescriptionOrder>> GetPagedWithDetailsAsync(
    int skip, int top, bool includeCount,
    string? orderBy, bool descending, CancellationToken ct)
{
    var query = _collection.Find(FilterDefinition<PrescriptionOrderDataModel>.Empty);

    // Apply sorting (with whitelist validation)
    if (!string.IsNullOrEmpty(orderBy))
    {
        if (!AllowedSortFields.Contains(orderBy))
            throw new ArgumentException($"Sorting by '{orderBy}' is not allowed");

        query = descending
            ? query.SortByDescending(GetSortExpression(orderBy))
            : query.SortBy(GetSortExpression(orderBy));
    }

    // Get total count if requested
    var totalCount = includeCount
        ? await _collection.CountDocumentsAsync(
            FilterDefinition<PrescriptionOrderDataModel>.Empty, ct)
        : 0;

    // Apply pagination and execute
    var items = await query.Skip(skip).Limit(top).ToListAsync(ct);

    // Map to domain entities
    var domainEntities = items.Select(MapToDomain).ToList();

    return new PagedData<PrescriptionOrder>(domainEntities, totalCount);
}

private static readonly HashSet<string> AllowedSortFields = new()
{
    "orderDate", "patientName", "medicationName", "status", "createdAt"
};
```

### 5. PagedResult Response Format

```csharp
public sealed class PagedResult<T>
{
    [JsonPropertyName("@odata.context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ODataContext { get; init; }

    [JsonPropertyName("@odata.count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ODataCount { get; init; }

    [JsonPropertyName("@odata.nextLink")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ODataNextLink { get; init; }

    [JsonPropertyName("value")]
    public required IReadOnlyList<T> Value { get; init; }
}
```

---

## Configuration

### appsettings.json

```json
{
  "Pagination": {
    "DefaultPageSize": 20,
    "MaxPageSize": 100,
    "IncludeCountByDefault": true
  }
}
```

### Startup Configuration

```csharp
// Program.cs
builder.Services.Configure<PaginationSettings>(
    builder.Configuration.GetSection(PaginationSettings.SectionName));

// Add OData for metadata endpoint only
builder.Services.AddControllers()
    .AddOData(options => options
        .Select()
        .Filter()
        .OrderBy()
        .Count()
        .SetMaxTop(100)
        .AddRouteComponents("odata", GetEdmModel()));

// EDM Model for $metadata endpoint
static IEdmModel GetEdmModel()
{
    var builder = new ODataConventionModelBuilder();
    builder.EntitySet<PrescriptionOrderDto>("Orders");
    builder.EntitySet<PatientDto>("Patients");
    builder.EntitySet<PrescriptionDto>("Prescriptions");
    return builder.GetEdmModel();
}
```

---

## Security Considerations

### 1. Field Whitelisting

Always validate sort fields against a whitelist:

```csharp
private static readonly HashSet<string> AllowedSortFields = new()
{
    "orderDate", "patientName", "medicationName", "status"
};

if (!string.IsNullOrEmpty(orderBy) && !AllowedSortFields.Contains(orderBy))
{
    throw new ArgumentException($"Sorting by '{orderBy}' is not allowed");
}
```

### 2. Max Page Size Enforcement

```csharp
public int GetEffectiveTop(PaginationSettings settings)
{
    var requestedTop = Top ?? settings.DefaultPageSize;
    return Math.Min(requestedTop, settings.MaxPageSize);  // Enforce max
}
```

### 3. Authorization

All queries go through MediatR handlers where authorization is enforced:

```csharp
public async Task<PagedData<PrescriptionOrder>> Handle(
    GetOrdersPagedQuery request, CancellationToken ct)
{
    // Authorization check
    if (!_currentUser.HasPermission(Permissions.ReadOrders))
        throw new UnauthorizedException();

    // Business logic
    return await _unitOfWork.PrescriptionOrders.GetPagedWithDetailsAsync(...);
}
```

---

## Performance Optimization

### 1. Conditional Counting

Counting can be expensive on large collections. Allow clients to opt out:

```http
GET /api/v2/orders?$top=20&$count=false
```

### 2. MongoDB Indexes

Create indexes for sortable fields:

```csharp
await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<PrescriptionOrderDataModel>(
        Builders<PrescriptionOrderDataModel>.IndexKeys.Descending(x => x.OrderDate)));
```

### 3. Caching in MediatR

Caching is transparent via the `ICacheableQuery` interface and `CachingBehavior` pipeline:

```csharp
// Query implements ICacheableQuery - caching is automatic
public sealed record GetOrdersPagedQuery(
    int Skip,
    int Top,
    bool IncludeCount = false,
    string? OrderBy = null,
    bool Descending = false) : IRequest<PagedData<PrescriptionOrder>>, ICacheableQuery
{
    // Cache key includes all parameters that affect the result
    public string CacheKey => $"orders:paged:{Skip}:{Top}:{OrderBy}:{Descending}";
}

// Handler has NO caching logic - CachingBehavior handles it
public class GetOrdersPagedHandler : IRequestHandler<GetOrdersPagedQuery, PagedData<PrescriptionOrder>>
{
    public async Task<PagedData<PrescriptionOrder>> Handle(
        GetOrdersPagedQuery request, CancellationToken ct)
    {
        // Just query database - caching is handled by CachingBehavior
        return await _unitOfWork.PrescriptionOrders.GetPagedWithDetailsAsync(...);
    }
}
```

See [Caching Strategy](./09-caching-strategy.md) for details on `ICacheableQuery` and cache invalidation.

---

## Testing

### Unit Test Example

```csharp
[Fact]
public async Task GetAll_WithODataParams_ShouldReturnPagedResult()
{
    // Arrange
    var query = new ODataQueryOptions
    {
        Top = 20,
        Skip = 40,
        Count = true,
        OrderBy = "orderDate desc"
    };

    var pagedData = new PagedData<PrescriptionOrder>(
        new List<PrescriptionOrder> { /* test data */ },
        totalCount: 150);

    _mediatorMock
        .Setup(m => m.Send(It.IsAny<GetOrdersPagedQuery>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(pagedData);

    // Act
    var result = await _controller.GetAll(query, CancellationToken.None);

    // Assert
    var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
    var pagedResult = okResult.Value.ShouldBeOfType<PagedResult<PrescriptionOrderDto>>();
    pagedResult.ODataCount.ShouldBe(150);
    pagedResult.ODataNextLink.ShouldNotBeNull();
    pagedResult.Value.Count.ShouldBe(20);
}
```

---

## Why This Approach is Better

### ✅ Advantages

1. **Industry Standard**: OData is an OASIS standard, widely supported
2. **Tool Support**: Power BI, Excel, Postman, OData clients work out of the box
3. **Clean Architecture**: All queries go through MediatR (business logic, validation, caching)
4. **MongoDB Compatible**: Simple queries (Skip, Limit, SortBy) work perfectly
5. **Secure**: Explicit parameters, field whitelisting, no arbitrary queries
6. **Performant**: Predictable performance, optional counting, caching support
7. **Scalable**: Works with millions of documents (with proper indexes)
8. **Modern**: OData v4 is actively maintained and evolving

### ❌ What We Avoid

1. **`[EnableQuery]` Bypass**: Maintains Clean Architecture
2. **Complex Queries**: No `$filter` with lambdas that MongoDB can't handle
3. **N+1 Queries**: No `$expand` that causes performance issues
4. **Schema Exposure**: No `$select` that breaks API versioning
5. **Arbitrary Queries**: No security risks from unvalidated queries

---

## Summary

This implementation provides:

- ✅ **OData query syntax**: `$top`, `$skip`, `$count`, `$orderby`
- ✅ **OData response format**: `@odata.context`, `@odata.count`, `@odata.nextLink`, `value`
- ✅ **OData metadata**: `/odata/$metadata` for tool discovery
- ✅ **Clean Architecture**: MediatR, business logic, validation, caching
- ✅ **MongoDB compatibility**: Simple queries that work perfectly
- ✅ **Security**: Field whitelisting, max page size, authorization
- ✅ **Performance**: Optional counting, caching, indexes
- ✅ **Scalability**: Works with large datasets

**This is the modern, safe, scalable way to implement OData with MongoDB and Clean Architecture!**



