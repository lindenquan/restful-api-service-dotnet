# Project Structure

## Overview

This project follows **Clean Architecture** with a **hybrid file organization**:

- **Entities Layer**: Core business entities (no dependencies)
- **Application Layer**: Use cases, operations (feature-based)
- **Adapters Layer**: Interface implementations (Api + Persistence)

```
src/
├── Entities/            # Core business entities (no dependencies)
├── Application/         # Use cases, operations (feature-based)
└── Adapters/            # Interface implementations
    ├── Api/             # HTTP layer, controllers, middleware
    └── Persistence/     # Database, cache, external services

tests/
├── Tests/               # Unit tests (mirrors src/ structure)
└── Tests.E2E/           # End-to-end integration tests

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
└── ApiKeyUser.cs           # API key authentication entity
```

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
│   ├── Shared/
│   │   ├── InternalOrderDto.cs    # Internal representation
│   │   └── EntityToInternalDto.cs # Mapper
│   ├── V1/                        # API Version 1
│   │   ├── DTOs/OrderDto.cs
│   │   └── Mappers/OrderMapper.cs
│   └── V2/                        # API Version 2
│       ├── DTOs/PrescriptionOrderDto.cs
│       └── Mappers/PrescriptionOrderMapper.cs
│
├── Users/                      # Feature: User Management
│   └── Operations/
│
├── Prescriptions/              # Feature: Prescription Management
│   └── Operations/
│
├── ApiKeys/                    # Feature: API Key Management (Admin)
│   └── Operations/
│
├── Behaviors/                  # MediatR Pipeline Behaviors
│   ├── LoggingBehavior.cs
│   └── ValidationBehavior.cs
│
├── Interfaces/                 # Abstractions for Infrastructure
│   ├── Repositories/
│   │   ├── IRepository.cs
│   │   └── IOrderRepository.cs
│   └── Services/
│       ├── ICacheService.cs
│       └── ICurrentUserService.cs
│
└── DependencyInjection.cs      # MediatR, FluentValidation registration
```

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

