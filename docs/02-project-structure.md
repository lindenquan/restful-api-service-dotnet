# Project Structure

## Overview

This project follows **Clean Architecture** with a **hybrid file organization**:

- **Entities Layer**: Core business entities (no dependencies)
- **Application Layer**: Use cases, operations (feature-based)
- **Adapters Layer**: Interface implementations (Api + Persistence)

```
src/
├── Entities/            # Core business entities (no dependencies)
├── DTOs/                # Shared DTOs for all API versions and adapters
├── Application/         # Use cases, operations (feature-based)
└── Adapters/            # Interface implementations
    ├── Api/             # HTTP layer, controllers, middleware
    └── Persistence/     # Database, cache, external services

tests/
├── Tests/          # Unit tests (business logic + L1 cache)
└── Tests.Api.E2E/  # API E2E tests (MongoDB + Redis integration)

tools/
└── DatabaseMigrations/  # MongoDB index creation

docs/                    # Documentation
```

---

## Layer Details

### Entities Layer (`src/Entities/`)

Pure business entities with no external dependencies.

```
Entities/
├── BaseEntity.cs           # Common fields (Id, CreatedAt, etc.)
├── PrescriptionOrder.cs    # Prescription Order aggregate
├── User.cs                 # User entity
├── Prescription.cs         # Prescription entity
├── Patient.cs              # Patient entity
└── ApiKeyUser.cs           # API key authentication entity
```

### DTOs Layer (`src/DTOs/`)

**Shared DTOs for all API versions and adapters** — independent project with no business logic.

```
DTOs/
├── Shared/                 # Shared DTOs across all API versions
│   └── UserDto.cs          # CreateUserRequest, CreateUserResponse
│
├── V1/                     # API Version 1 DTOs
│   ├── OrderDto.cs         # V1 Order response
│   ├── CreateOrderRequest.cs
│   └── UpdateOrderRequest.cs
│
└── V2/                     # API Version 2 DTOs
    ├── PrescriptionOrderDto.cs  # V2 Order response
    ├── CreatePrescriptionOrderRequest.cs
    └── UpdatePrescriptionOrderRequest.cs
```

**Key Benefits:**
- ✅ **Shared across adapters** - Can be used by API, gRPC, GraphQL, etc.
- ✅ **Version isolation** - V1 and V2 DTOs are completely separate
- ✅ **No business logic** - Pure data transfer objects
- ✅ **Independent deployment** - Can be packaged as NuGet for clients

### Application Layer (`src/Application/`)

**Feature-based organization** — each feature folder contains everything related to that use case:

```
Application/
├── Orders/                     # Feature: Order Management
│   ├── Operations/
│   │   ├── CreateOrder.cs      # Request + Handler in one file
│   │   ├── CreateOrderValidator.cs
│   │   ├── UpdateOrderStatus.cs
│   │   ├── CancelOrder.cs
│   │   ├── DeleteOrder.cs
│   │   ├── GetOrderById.cs
│   │   ├── GetAllOrders.cs
│   │   └── GetOrdersByUser.cs
│   └── Shared/
│       ├── InternalOrderDto.cs    # Internal representation (Status: OrderStatus enum)
│       └── EntityToInternalDto.cs # Entity → Internal DTO mapper
│
├── Users/                      # Feature: User Management
│   └── Operations/
│
├── Prescriptions/              # Feature: Prescription Management
│   ├── Operations/
│   └── Shared/
│       ├── InternalPrescriptionDto.cs  # Internal representation
│       └── EntityToInternalDto.cs      # Entity → Internal DTO mapper
│
├── ApiKeys/                    # Feature: API Key Management (Admin)
│   └── Operations/
│
├── Behaviors/                  # MediatR Pipeline Behaviors
│   ├── LoggingBehavior.cs
│   ├── ValidationBehavior.cs
│   └── CachingBehavior.cs
│
├── Interfaces/                 # Abstractions for Infrastructure
│   ├── Repositories/
│   │   ├── IRepository.cs
│   │   ├── IUnitOfWork.cs
│   │   └── IOrderRepository.cs
│   └── Services/
│       ├── ICacheService.cs
│       └── ICurrentUserService.cs
│
└── DependencyInjection.cs      # MediatR, FluentValidation registration
```

**Key Principles:**
- ✅ **Consistent structure** - Every feature has `Operations/` and `Shared/`
- ✅ **Internal DTOs in Shared/** - `InternalXxxDto` for each feature
- ✅ **Type safety** - Internal DTOs use enums (e.g., `OrderStatus` not string)
- ✅ **Mappers in Shared/** - `EntityToInternalDto` for each feature
- ✅ **External DTOs separate** - Versioned DTOs in `src/DTOs` project
- ✅ **Cross-version DTOs** - Shared DTOs in `src/DTOs/Shared/` for common types

### Adapters Layer (`src/Adapters/`)

The Adapters layer combines **Api** (driving adapters) and **Persistence** (driven adapters):

```
Adapters/
├── Api/                        # HTTP/Web adapters (driving)
│   ├── Controllers/
│   │   ├── V1/                 # API Version 1
│   │   │   ├── OrdersController.cs
│   │   │   └── AdminController.cs
│   │   └── V2/                 # API Version 2
│   │       └── OrdersController.cs
│   │
│   ├── Authentication/
│   │   └── ApiKeyAuthenticationHandler.cs
│   │
│   ├── Authorization/
│   │   └── AuthorizationPolicies.cs
│   │
│   ├── Middleware/
│   │   ├── GlobalExceptionMiddleware.cs
│   │   ├── SecurityHeadersMiddleware.cs
│   │   └── ValidationExceptionMiddleware.cs
│   │
│   ├── Services/
│   │   └── CurrentUserService.cs
│   │
│   ├── appsettings.json        # Base configuration
│   ├── appsettings.dev.json    # Development
│   ├── appsettings.prod.json   # Production
│   ├── appsettings.stage.json  # Staging
│   ├── appsettings.e2e.json    # E2E Testing
│   ├── appsettings.amr-prod.json
│   │
│   ├── Program.cs              # Application entry point
│   └── Dockerfile
│
└── Persistence/                # Data/External adapters (driven)
    ├── Repositories/
    │   ├── Repository.cs       # Generic InMemory repository
    │   ├── OrderRepository.cs
    │   ├── MongoDB/            # MongoDB-specific implementations
    │   │   ├── MongoOrderRepository.cs
    │   │   └── MongoUnitOfWork.cs
    │   └── UnitOfWork.cs
    │
    ├── Services/
    │   ├── MemoryCacheService.cs
    │   ├── RedisCacheService.cs
    │   ├── RootAdminInitializer.cs
    │   └── SlidingWindowRateLimiter.cs
    │
    ├── Security/
    │   └── ApiKeyHasher.cs
    │
    ├── Configuration/
    │   ├── DatabaseSettings.cs
    │   ├── MongoDbSettings.cs
    │   ├── RedisSettings.cs
    │   ├── CorsSettings.cs
    │   └── RateLimitSettings.cs
    │
    └── DependencyInjection.cs
```

---

## Why This Hybrid Approach?

| Layer | Organization | Reason |
|-------|--------------|--------|
| **Entities** | Entity-based | Core business objects, no dependencies |
| **Application** | Feature-based | Each use case is self-contained. Easy to find all related code. |
| **Adapters** | Layer-based | Technical concerns (DB, Cache, HTTP) are shared across features. |

---

## Navigation Tips

| I want to... | Go to... |
|--------------|----------|
| Add a new order operation | `Application/Orders/Operations/` |
| Add validation for an operation | `Application/Orders/Operations/XxxValidator.cs` |
| Change database implementation | `Adapters/Persistence/Repositories/MongoDB/` |
| Add a new external service | `Adapters/Persistence/Services/` |
| Add API versioned DTO | `Application/Orders/V1/DTOs/` or `V2/DTOs/` |
| Add middleware | `Adapters/Api/Middleware/` |
| Configure environment | `Adapters/Api/appsettings.{env}.json` |

