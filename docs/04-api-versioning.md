# API Versioning

## Overview

This API supports **multiple versions** to allow backward-compatible evolution. Clients can specify the version via:

- URL path: `/api/v1/orders` or `/api/v2/orders`
- Header: `X-Api-Version: 1.0` (optional)

---

## Version Strategy

| Version | Route | Description |
|---------|-------|-------------|
| V1 | `/api/v1/orders` | Original API with simplified DTOs |
| V2 | `/api/v2/orders` | Enhanced API with detailed DTOs |

---

## DTO Mapping Architecture

The system uses a **three-layer DTO mapping** strategy:

```
┌─────────────────────────────────────────────────────────────┐
│                    External Layer (API)                      │
│  ┌─────────────────┐              ┌─────────────────┐       │
│  │  V1/OrderDto    │              │  V2/OrderDto    │       │
│  │  (simplified)   │              │  (detailed)     │       │
│  └────────┬────────┘              └────────┬────────┘       │
│           │                                │                 │
│           │ OrderMapper.ToDto()            │ OrderMapper.ToDto()
│           │                                │                 │
├───────────▼────────────────────────────────▼─────────────────┤
│                   Internal Layer (Application)               │
│                    ┌─────────────────┐                       │
│                    │ InternalOrderDto│ ← Single internal     │
│                    │ (all fields)    │   representation      │
│                    └────────┬────────┘                       │
│                             │                                │
│                             │ EntityToInternalDto.Map()      │
│                             │                                │
├─────────────────────────────▼────────────────────────────────┤
│                    Domain Layer                              │
│                    ┌─────────────────┐                       │
│                    │  Order Entity   │                       │
│                    │  (database)     │                       │
│                    └─────────────────┘                       │
└─────────────────────────────────────────────────────────────┘
```

---

## File Structure

```
Application/Orders/
├── Shared/
│   ├── InternalOrderDto.cs       # Internal representation (all fields)
│   └── EntityToInternalDto.cs    # Entity → Internal mapper
│
├── V1/
│   ├── DTOs/
│   │   └── OrderDto.cs           # V1 external DTO (simplified)
│   └── Mappers/
│       └── OrderMapper.cs        # Internal → V1 DTO
│
└── V2/
    ├── DTOs/
    │   └── PrescriptionOrderDto.cs  # V2 external DTO (detailed)
    └── Mappers/
        └── PrescriptionOrderMapper.cs  # Internal → V2 DTO
```

---

## Example: V1 vs V2 DTOs

### V1 DTO (Simplified)

```csharp
// Application/Orders/V1/DTOs/OrderDto.cs
public record OrderDto(
    Guid Id,
    string Status,
    DateTime CreatedAt
);
```

### V2 DTO (Detailed)

```csharp
// Application/Orders/V2/DTOs/PrescriptionOrderDto.cs
public record PrescriptionOrderDto(
    Guid Id,
    Guid UserId,
    Guid PrescriptionId,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string CreatedBy,
    string? UpdatedBy
);
```

### Internal DTO (Complete)

```csharp
// Application/Orders/Shared/InternalOrderDto.cs
public record InternalOrderDto(
    Guid Id,
    Guid UserId,
    Guid PrescriptionId,
    OrderStatus Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string CreatedBy,
    string? UpdatedBy,
    bool IsDeleted,
    DateTime? DeletedAt,
    string? DeletedBy
);
```

---

## Mappers

### Entity → Internal

```csharp
// Application/Orders/Shared/EntityToInternalDto.cs
public static class EntityToInternalDto
{
    public static InternalOrderDto Map(Order entity) => new(
        entity.Id,
        entity.UserId,
        entity.PrescriptionId,
        entity.Status,
        entity.Notes,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.CreatedBy,
        entity.UpdatedBy,
        entity.IsDeleted,
        entity.DeletedAt,
        entity.DeletedBy
    );
}
```

### Internal → V1 External

```csharp
// Application/Orders/V1/Mappers/OrderMapper.cs
public static class OrderMapper
{
    public static OrderDto ToDto(InternalOrderDto order) => new(
        order.Id,
        order.Status.ToString(),  // Enum to string
        order.CreatedAt
    );
}
```

### Internal → V2 External

```csharp
// Application/Orders/V2/Mappers/PrescriptionOrderMapper.cs
public static class PrescriptionOrderMapper
{
    public static PrescriptionOrderDto ToDto(InternalOrderDto order) => new(
        order.Id,
        order.UserId,
        order.PrescriptionId,
        order.Status.ToString(),
        order.Notes,
        order.CreatedAt,
        order.UpdatedAt,
        order.CreatedBy,
        order.UpdatedBy
    );
}
```

---

## Controller Usage

### V1 Controller

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
public class OrdersController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var internal = await _mediator.Send(new GetOrderByIdQuery(id));
        if (internal is null) return NotFound();
        
        return Ok(V1.Mappers.OrderMapper.ToDto(internal));  // V1 mapping
    }
}
```

### V2 Controller

```csharp
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/orders")]
public class OrdersController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var internal = await _mediator.Send(new GetOrderByIdQuery(id));
        if (internal is null) return NotFound();
        
        return Ok(V2.Mappers.PrescriptionOrderMapper.ToDto(internal));  // V2 mapping
    }
}
```

---

## Benefits of This Approach

| Benefit | Description |
|---------|-------------|
| **Single Source of Truth** | Handlers return `InternalOrderDto`, not version-specific DTOs |
| **Easy Version Addition** | Add V3 folder with new DTOs and mappers |
| **No Handler Duplication** | Same query handler serves all versions |
| **Clear Separation** | External contract separate from internal model |
| **Backward Compatibility** | V1 clients continue working when V2 is released |

