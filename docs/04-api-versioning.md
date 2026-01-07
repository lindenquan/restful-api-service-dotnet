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

The system uses a **two-layer DTO mapping** strategy:

```　
┌──────────────────────────────────────────────────────────────┐
│                    External Layer (API)                      │
│  ┌─────────────────┐              ┌─────────────────────┐    │
│  │  V1/OrderDto    │              │  V2/PrescriptionOrderDto │
│  │  (simplified)   │              │  (detailed)              │
│  └────────┬────────┘              └────────┬────────────┘    │
│           │                                │                 │
│           │ OrderMapper.ToV1Dto()          │ PrescriptionOrderMapper.ToV2Dto()
│           │                                │                 │
├───────────▼────────────────────────────────▼─────────────────┤
│                    Domain Layer                              │
│                    ┌──────────────────┐                      │
│                    │ PrescriptionOrder│ ← Domain entity      │
│                    │  (returned by    │   returned by        │
│                    │   handlers)      │   MediatR handlers   │
│                    └──────────────────┘                      │
└──────────────────────────────────────────────────────────────┘
```

**Key Design Decisions:**
- ✅ **Handlers return domain entities** - No intermediate DTO layer needed
- ✅ **Mappers in Infrastructure** - Conversion from entity → versioned DTO at the boundary
- ✅ **Type-safe domain** - Domain uses `OrderStatus` enum, mappers convert to string

---

## File Structure

```
src/
├── DTOs/                         # Shared DTOs project (independent)
│   ├── V1/
│   │   └── OrderDto.cs           # V1 DTOs (OrderDto, CreateOrderRequest, UpdateOrderRequest)
│   │
│   ├── V2/
│   │   ├── PatientDto.cs            # V2 Patient DTOs
│   │   ├── PrescriptionDto.cs       # V2 Prescription DTOs
│   │   └── PrescriptionOrderDto.cs  # V2 Order DTOs (detailed)
│   │
│   └── Shared/
│       └── PaginationDto.cs         # Shared pagination models
│
├── Domain/
│   └── PrescriptionOrder.cs         # Domain entity (uses OrderStatus enum)
│
└── Infrastructure/Api/Controllers/
    ├── V1/Mappers/
    │   └── OrderMapper.cs           # Entity → V1 DTO (enum → string)
    │
    └── V2/Mappers/
        ├── PatientMapper.cs         # Entity → V2 Patient DTO
        ├── PrescriptionMapper.cs    # Entity → V2 Prescription DTO
        └── PrescriptionOrderMapper.cs  # Entity → V2 Order DTO
```

**Key Benefits:**
- ✅ **DTOs in separate project** - Can be shared across all adapters (API, gRPC, etc.)
- ✅ **Handlers return domain entities** - No intermediate DTO layer needed
- ✅ **Mappers in Infrastructure** - Conversion logic at the boundary (enum → string)

---

## Example: V1 vs V2 DTOs

### V1 DTO (Simplified)

```csharp
// src/DTOs/V1/OrderDto.cs
public record OrderDto(
    Guid Id,
    Guid PatientId,
    string CustomerName,      // V1 naming: CustomerName
    Guid PrescriptionId,
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
    Guid Id,
    Guid PatientId,
    string PatientName,       // V2 naming: PatientName
    Guid PrescriptionId,
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

### Domain Entity (Type-Safe)

```csharp
// src/Domain/PrescriptionOrder.cs
public class PrescriptionOrder : BaseEntity
{
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }  // ← Enum for type safety
    public string? Notes { get; set; }
    public DateTime? FulfilledDate { get; set; }
    public DateTime? PickupDate { get; set; }
    public Guid PatientId { get; set; }
    public Guid PrescriptionId { get; set; }
    public Patient? Patient { get; set; }
    public Prescription? Prescription { get; set; }
}
```

**Key Difference:**
- ✅ **External DTOs** (V1, V2): `Status` is `string` for API flexibility
- ✅ **Domain Entity**: `Status` is `OrderStatus` enum for type safety
- ✅ **Mappers** handle the conversion at the infrastructure boundary

---

## Mappers

### Entity → V1 DTO (Infrastructure Layer)

```csharp
// src/Infrastructure/Api/Controllers/V1/Mappers/OrderMapper.cs
public static class OrderMapper
{
    public static OrderDto ToV1Dto(PrescriptionOrder order) => new(
        Id: order.Id,
        PatientId: order.PatientId,
        CustomerName: order.Patient?.FullName ?? "Unknown",
        PrescriptionId: order.PrescriptionId,
        Medication: order.Prescription?.MedicationName ?? "Unknown",
        OrderDate: order.OrderDate,
        Status: order.Status.ToString(),  // ← Convert enum to string
        Notes: order.Notes
    );
}
```

### Entity → V2 DTO (Infrastructure Layer)

```csharp
// src/Infrastructure/Api/Controllers/V2/Mappers/PrescriptionOrderMapper.cs
public static class PrescriptionOrderMapper
{
    public static PrescriptionOrderDto ToV2Dto(PrescriptionOrder order) => new(
        Id: order.Id,
        PatientId: order.PatientId,
        PatientName: order.Patient?.FullName ?? "Unknown",
        PrescriptionId: order.PrescriptionId,
        MedicationName: order.Prescription?.MedicationName ?? "Unknown",
        Dosage: order.Prescription?.Dosage ?? "",
        OrderDate: order.OrderDate,
        Status: order.Status.ToString(),  // ← Convert enum to string
        Notes: order.Notes,
        FulfilledDate: order.FulfilledDate,
        PickupDate: order.PickupDate,
        CreatedAt: order.CreatedAt,
        UpdatedAt: order.UpdatedAt
    );
}
```

**Mapping Flow:**
```
PrescriptionOrder (Domain Entity with OrderStatus enum)
    ↓ OrderMapper.ToV1Dto()          ↓ PrescriptionOrderMapper.ToV2Dto()
V1 OrderDto (string Status)      V2 PrescriptionOrderDto (string Status)
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
        var order = await _mediator.Send(new GetOrderByIdQuery(id));
        if (order is null) return NotFound();

        return Ok(OrderMapper.ToV1Dto(order));  // V1 mapping
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
        var order = await _mediator.Send(new GetOrderByIdQuery(id));
        if (order is null) return NotFound();

        return Ok(PrescriptionOrderMapper.ToV2Dto(order));  // V2 mapping
    }
}
```

---

## Benefits of This Approach

| Benefit | Description |
|---------|-------------|
| **Single Source of Truth** | Handlers return domain entities, not version-specific DTOs |
| **Easy Version Addition** | Add V3 folder with new DTOs and mappers |
| **No Handler Duplication** | Same query handler serves all versions |
| **Clear Separation** | External contract separate from domain model |
| **Backward Compatibility** | V1 clients continue working when V2 is released |

