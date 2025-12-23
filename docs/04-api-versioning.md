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
src/
├── DTOs/                         # Shared DTOs project (independent)
│   ├── V1/
│   │   ├── OrderDto.cs           # V1 external DTO (simplified)
│   │   ├── CreateOrderRequest.cs
│   │   └── UpdateOrderRequest.cs
│   │
│   └── V2/
│       ├── PrescriptionOrderDto.cs  # V2 external DTO (detailed)
│       ├── CreatePrescriptionOrderRequest.cs
│       └── UpdatePrescriptionOrderRequest.cs
│
├── Application/Orders/
│   └── Shared/
│       ├── InternalOrderDto.cs       # Internal DTO (Status: OrderStatus enum)
│       └── EntityToInternalDto.cs    # Entity → Internal mapper
│
└── Adapters/Api/Controllers/
    ├── V1/Mappers/
    │   └── OrderMapper.cs            # Internal DTO → V1 DTO (enum → string)
    │
    └── V2/Mappers/
        └── PrescriptionOrderMapper.cs  # Internal DTO → V2 DTO (enum → string)
```

**Key Improvements:**
- ✅ **DTOs in separate project** - Can be shared across all adapters (API, gRPC, etc.)
- ✅ **Type-safe InternalOrderDto** - Uses `OrderStatus` enum (not string)
- ✅ **Mappers in Adapters** - Conversion logic at the boundary (enum → string)

---

## Example: V1 vs V2 DTOs

### V1 DTO (Simplified)

```csharp
// src/DTOs/V1/OrderDto.cs
public record OrderDto(
    int Id,
    int PatientId,
    string CustomerName,      // V1 naming: CustomerName
    int PrescriptionId,
    string Medication,        // V1 naming: Medication
    DateTime OrderDate,
    string Status,            // ← String for external API
    string? Notes
);
```

### V2 DTO (Detailed)

```csharp
// src/DTOs/V2/PrescriptionOrderDto.cs
public record PrescriptionOrderDto(
    int Id,
    int PatientId,
    string PatientName,       // V2 naming: PatientName
    int PrescriptionId,
    string MedicationName,    // V2 naming: MedicationName
    string Dosage,            // V2 includes dosage
    DateTime OrderDate,
    string Status,            // ← String for external API
    string? Notes,
    DateTime? FulfilledDate,  // V2 includes fulfillment tracking
    DateTime? PickupDate,     // V2 includes pickup tracking
    DateTime CreatedAt,       // V2 includes audit fields
    DateTime? UpdatedAt
);
```

### Internal DTO (Complete, Type-Safe)

```csharp
// src/Application/Orders/Shared/InternalOrderDto.cs
public record InternalOrderDto(
    int Id,
    int PatientId,
    string PatientName,
    int PrescriptionId,
    string MedicationName,
    string Dosage,
    DateTime OrderDate,
    OrderStatus Status,       // ← Enum for type safety (not string!)
    string? Notes,
    DateTime? FulfilledDate,
    DateTime? PickupDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
```

**Key Difference:**
- ✅ **External DTOs** (V1, V2): `Status` is `string` for API flexibility
- ✅ **Internal DTO**: `Status` is `OrderStatus` enum for type safety
- ✅ **Mappers** handle the conversion at the adapter boundary

---

## Mappers

### Entity → Internal DTO (Application Layer)

```csharp
// src/Application/Orders/Shared/EntityToInternalDto.cs
public static class EntityToInternalDto
{
    public static InternalOrderDto Map(PrescriptionOrder order) => new(
        Id: order.Id,
        PatientId: order.PatientId,
        PatientName: order.Patient?.FullName ?? "Unknown",
        PrescriptionId: order.PrescriptionId,
        MedicationName: order.Prescription?.MedicationName ?? "Unknown",
        Dosage: order.Prescription?.Dosage ?? "",
        OrderDate: order.OrderDate,
        Status: order.Status,  // ← Keep as enum (type-safe!)
        Notes: order.Notes,
        FulfilledDate: order.FulfilledDate,
        PickupDate: order.PickupDate,
        CreatedAt: order.CreatedAt,
        UpdatedAt: order.UpdatedAt
    );
}
```

### Internal DTO → V1 DTO (Adapter Layer)

```csharp
// src/Adapters/Api/Controllers/V1/Mappers/OrderMapper.cs
public static class OrderMapper
{
    public static OrderDto ToV1Dto(InternalOrderDto internalDto) => new(
        Id: internalDto.Id,
        PatientId: internalDto.PatientId,
        CustomerName: internalDto.PatientName,
        PrescriptionId: internalDto.PrescriptionId,
        Medication: internalDto.MedicationName,
        OrderDate: internalDto.OrderDate,
        Status: internalDto.Status.ToString(),  // ← Convert enum to string
        Notes: internalDto.Notes
    );
}
```

### Internal DTO → V2 DTO (Adapter Layer)

```csharp
// src/Adapters/Api/Controllers/V2/Mappers/PrescriptionOrderMapper.cs
public static class PrescriptionOrderMapper
{
    public static PrescriptionOrderDto ToV2Dto(InternalOrderDto internalDto) => new(
        Id: internalDto.Id,
        PatientId: internalDto.PatientId,
        PatientName: internalDto.PatientName,
        PrescriptionId: internalDto.PrescriptionId,
        MedicationName: internalDto.MedicationName,
        Dosage: internalDto.Dosage,
        OrderDate: internalDto.OrderDate,
        Status: internalDto.Status.ToString(),  // ← Convert enum to string
        Notes: internalDto.Notes,
        FulfilledDate: internalDto.FulfilledDate,
        PickupDate: internalDto.PickupDate,
        CreatedAt: internalDto.CreatedAt,
        UpdatedAt: internalDto.UpdatedAt
    );
}
```

**Mapping Flow:**
```
Entity (OrderStatus enum)
    ↓ EntityToInternalDto.Map()
InternalOrderDto (OrderStatus enum) ← Type-safe in Application layer
    ↓ OrderMapper.ToV1Dto() / PrescriptionOrderMapper.ToV2Dto()
External DTOs (string) ← Converted at adapter boundary
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

