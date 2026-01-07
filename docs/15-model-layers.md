# Model Layers and Mapping Strategy

This document describes the different model types across application layers, their purposes, and how they are mapped between layers.

## Overview

The application uses multiple model types to maintain clean separation of concerns:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              API Layer                                      │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐          │
│  │   V1 DTOs       │    │   V2 DTOs       │    │  Shared DTOs    │          │
│  │  OrderDto       │    │ PrescriptionDto │    │  UserDto        │          │
│  │  PatientDto     │    │  OrderDto       │    │                 │          │
│  └────────┬────────┘    └────────┬────────┘    └────────┬────────┘          │
└───────────┼──────────────────────┼──────────────────────┼───────────────────┘
            │                      │                      │
            │    V1/V2 Mappers     │                      │
            ▼                      ▼                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Application Layer                                  │
│  ┌─────────────────────────────────────────────────────────────────┐        │
│  │                      Domain Entities                            │        │
│  │  Patient, Prescription, PrescriptionOrder, User                 │        │
│  │  (Handlers return Domain entities directly)                     │        │
│  └────────────────────────────────┬────────────────────────────────┘        │
└───────────────────────────────────┼─────────────────────────────────────────┘
                                    │
                                    │  Persistence Mappers
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Infrastructure Layer                                 │
│  ┌─────────────────────────────────────────────────────────────────┐        │
│  │                      Data Models                                │        │
│  │  PatientDataModel, PrescriptionDataModel, etc.                  │        │
│  │  (MongoDB documents with Metadata sub-document)                 │        │
│  └─────────────────────────────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Layer-by-Layer Model Details

### 1. Domain Layer (`src/Domain/`)

**Purpose**: Pure business entities with domain logic. No infrastructure concerns.

| Entity | File | Description |
|--------|------|-------------|
| `BaseEntity` | `BaseEntity.cs` | Base with `Id` (UUID v7), audit properties (`CreatedAt`, `CreatedBy`, etc.) |
| `Patient` | `Patient.cs` | Patient with name, contact info, computed `FullName` |
| `Prescription` | `Prescription.cs` | Prescription with medication, refills, computed `IsExpired`, `CanRefill` |
| `PrescriptionOrder` | `PrescriptionOrder.cs` | Order linking Patient + Prescription with `OrderStatus` enum |
| `User` | `User.cs` | API user with `ApiKeyHash`, `UserType` enum |

**Key Characteristics**:
- ✅ No dependencies on other layers
- ✅ Contains business logic (computed properties, validation)
- ✅ Uses enums for type safety (`OrderStatus`, `UserType`)
- ✅ Audit properties for tracking changes
- ✅ Uses UUID v7 for IDs (time-sortable, globally unique)

**ID Strategy - UUID v7**:
All entity IDs use UUID v7 (`Guid.CreateVersion7()`) which provides:
- **Time-sortable**: UUIDs are ordered by creation time
- **Globally unique**: No coordination needed across distributed systems
- **Database-friendly**: Better index performance than random UUIDs
- **No enumeration attacks**: IDs are not predictable like sequential integers

---

### 2. API Layer DTOs (`src/DTOs/`)

**Purpose**: External API contracts. Versioned for backward compatibility.

#### V1 DTOs (`src/DTOs/V1/`)
| DTO | Description |
|-----|-------------|
| `OrderDto` | Simpler field names (`CustomerName`, `Medication`) |
| `CreateOrderRequest` | Create order request |
| `UpdateOrderRequest` | Update with status only |
| `PatientDto` | Basic patient info |
| `PrescriptionDto` | Basic prescription info |

#### V2 DTOs (`src/DTOs/V2/`)
| DTO | Description |
|-----|-------------|
| `PrescriptionOrderDto` | Detailed with `PatientName`, `MedicationName` |
| `CreatePrescriptionOrderRequest` | Create order request |
| `UpdatePrescriptionOrderRequest` | Update with status + notes |
| `PatientDto` | Includes `Age`, audit fields |
| `PrescriptionDto` | Includes `DaysUntilExpiry`, status indicators |

#### Shared DTOs (`src/DTOs/Shared/`)
| DTO | Description |
|-----|-------------|
| `CreateUserRequest` | User creation (shared across versions) |
| `CreateUserResponse` | User creation response |
| `UserDto` | User info response |

**Key Characteristics**:
- ✅ Version-specific contracts
- ✅ Strings for enums (API serialization)
- ✅ Different field names per version for compatibility
- ✅ No business logic

---

### 4. Infrastructure Data Models (`src/Infrastructure/Persistence/Models/`)

**Purpose**: MongoDB document structure with persistence-specific concerns (metadata, soft delete).

| Data Model | File | Description |
|------------|------|-------------|
| `BaseDataModel` | `BaseDataModel.cs` | Base with `Id`, `DataModelMetadata` sub-document |
| `PatientDataModel` | `PatientDataModel.cs` | Patient MongoDB document |
| `PrescriptionDataModel` | `PrescriptionDataModel.cs` | Prescription MongoDB document |
| `PrescriptionOrderDataModel` | `PrescriptionOrderDataModel.cs` | Order MongoDB document |
| `UserDataModel` | `UserDataModel.cs` | User MongoDB document |

**DataModelMetadata Structure**:
```csharp
public class DataModelMetadata
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }        // Soft delete flag
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

**Key Characteristics**:
- ✅ MongoDB-specific (BSON serialization)
- ✅ Metadata sub-document for audit/soft-delete
- ✅ No business logic
- ✅ Mapped FROM/TO Domain entities via persistence mappers

---

## Mapping Summary

### Mapping Flow

```
API Request DTO → Command/Query → Handler → Domain Entity → Data Model → MongoDB
                                                ↓
API Response DTO ← V1/V2 Mapper ← Handler ← Domain Entity ← Data Model ← MongoDB
```

### Mappers by Layer

| Layer | Mapper | Source → Target | Location |
|-------|--------|-----------------|----------|
| **Infrastructure** | `PatientPersistenceMapper` | Domain ↔ Data Model | `src/Infrastructure/Persistence/Mappers/` |
| **Infrastructure** | `PrescriptionPersistenceMapper` | Domain ↔ Data Model | `src/Infrastructure/Persistence/Mappers/` |
| **Infrastructure** | `PrescriptionOrderPersistenceMapper` | Domain ↔ Data Model | `src/Infrastructure/Persistence/Mappers/` |
| **Infrastructure** | `UserPersistenceMapper` | Domain ↔ Data Model | `src/Infrastructure/Persistence/Mappers/` |
| **API V1** | `PatientMapper` | Domain Entity → V1 DTO | `src/Infrastructure/Api/Controllers/V1/Mappers/` |
| **API V1** | `PrescriptionMapper` | Domain Entity → V1 DTO | `src/Infrastructure/Api/Controllers/V1/Mappers/` |
| **API V1** | `OrderMapper` | Domain Entity → V1 DTO | `src/Infrastructure/Api/Controllers/V1/Mappers/` |
| **API V2** | `PatientMapper` | Domain Entity → V2 DTO | `src/Infrastructure/Api/Controllers/V2/Mappers/` |
| **API V2** | `PrescriptionMapper` | Domain Entity → V2 DTO | `src/Infrastructure/Api/Controllers/V2/Mappers/` |
| **API V2** | `OrderMapper` | Domain Entity → V2 DTO | `src/Infrastructure/Api/Controllers/V2/Mappers/` |

### Mapping Technology

The persistence mappers use **[Mapperly](https://mapperly.riok.app/)** - a compile-time source generator for object mappings:

- ✅ **No runtime reflection** - mapping code generated at compile time
- ✅ **Type-safe** - compile errors for unmappable properties
- ✅ **Readable** - generated code can be inspected
- ✅ **Fast** - zero runtime overhead

---

## Why Multiple Model Types?

| Concern | Solution |
|---------|----------|
| **API versioning** | Separate V1/V2 DTOs allow breaking changes without breaking clients |
| **Type safety** | Domain entities use enums; API DTOs use strings for JSON |
| **Persistence isolation** | Data models handle MongoDB concerns (metadata, soft delete) |
| **Domain purity** | Domain entities have no infrastructure dependencies |
| **Testability** | Each layer can be tested independently |
| **Security** | UUID v7 IDs prevent enumeration attacks |

---

## Quick Reference

### To add a new entity:

1. **Domain**: Create `src/Domain/NewEntity.cs` extending `BaseEntity` (ID is auto-generated UUID v7)
2. **Data Model**: Create `src/Infrastructure/Persistence/Models/NewEntityDataModel.cs`
3. **Persistence Mapper**: Create `src/Infrastructure/Persistence/Mappers/NewEntityPersistenceMapper.cs`
4. **Repository**: Create repository implementing the mapper
5. **API DTOs**: Create `src/DTOs/V1/NewEntityDto.cs` and `src/DTOs/V2/NewEntityDto.cs`
6. **V1/V2 Mappers**: Create mappers in `src/Infrastructure/Api/Controllers/V1/Mappers/` and `V2/Mappers/`
