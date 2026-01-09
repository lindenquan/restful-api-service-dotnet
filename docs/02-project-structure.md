# Project Structure

## Overview

This project follows **Clean Architecture** with a **hybrid file organization**:

- **Domain Layer**: Core business entities (Enterprise Business Rules - no dependencies)
- **Application Layer**: Use cases, operations (Application Business Rules - feature-based)
- **Infrastructure Layer**: Interface implementations (Interface Adapters - Api + Persistence)

```
src/
├── Domain/              # Enterprise Business Rules (no dependencies)
├── DTOs/                # Shared DTOs for all API versions and adapters
├── Application/         # Application Business Rules (feature-based)
└── Infrastructure/      # Interface Adapters
    ├── Api/             # HTTP layer, controllers, middleware
    ├── Cache/           # Local/Remote cache implementations
    └── Persistence/     # Database, external services

tests/
├── Tests/          # Unit tests (business logic + Local cache)
└── Tests.Api.E2E/  # API E2E tests (MongoDB + Redis integration)

tools/
└── DatabaseMigrations/  # MongoDB index creation

docs/                    # Documentation
```

---

## Why Multiple Projects? (Compile-Time Enforcement)

We use **separate `.csproj` files** for each layer to get **compile-time dependency enforcement**.

### The Problem with Single-Project

In a single-project structure, nothing stops a developer from doing this:

```csharp
// File: Application/Users/CreateUserHandler.cs

using MongoDB.Driver;  // ← Nothing prevents this!

public class CreateUserHandler
{
    private readonly IMongoCollection<User> _collection;  // ← Direct MongoDB usage in Application layer
}
```

This **builds successfully** but violates Clean Architecture — Application layer should not know about MongoDB.

### How Multiple Projects Enforce Boundaries

```
Domain.csproj
  └── References: NOTHING

Application.csproj
  └── References: Domain.csproj only
  └── Packages: MediatR, FluentValidation (no infrastructure)

Infrastructure.csproj
  └── References: Application.csproj, Domain.csproj
  └── Packages: MongoDB.Driver, StackExchange.Redis
```

Now if a developer tries the same thing:

```csharp
// File: src/Application/Users/CreateUserHandler.cs

using MongoDB.Driver;  // ← Attempt to use MongoDB
```

**Result:**
```
❌ Build Error CS0246: The type or namespace 'MongoDB' could not be found
```

The **compiler physically prevents** the violation. No code review needed.

### What Each Project Can Access

| Project | Can Reference | Cannot Reference |
|---------|---------------|------------------|
| **Domain** | .NET base classes only | Application ❌, Infrastructure ❌, MongoDB ❌ |
| **Application** | Domain ✅ | Infrastructure ❌, MongoDB ❌, Redis ❌ |
| **Infrastructure** | Domain ✅, Application ✅, MongoDB ✅, Redis ✅ | — |

### Multi-Project vs Single-Project

| Aspect | Multi-Project | Single-Project |
|--------|---------------|----------------|
| **Boundary enforcement** | ✅ Compile-time (automatic) | ❌ Discipline only (manual) |
| **Accidental violations** | Impossible | Easy to make |
| **Code reviews** | Less critical for boundaries | Must catch violations |
| **Junior developer mistakes** | Compiler catches them | Slip through to production |
| **Setup complexity** | More `.csproj` files | Simpler |

### The Trade-off

**Multi-project = Compiler is your police.**
**Single-project = You are your own police.**

For long-term, team-based projects, we choose compiler enforcement.

---

## Layer Details

### Domain Layer (`src/Domain/`)

Pure business entities with no external dependencies (Enterprise Business Rules).

```
Domain/
├── BaseEntity.cs           # Common fields (Id, CreatedAt, etc.)
├── PrescriptionOrder.cs    # Prescription Order aggregate
├── User.cs                 # User entity (includes API key authentication)
├── Prescription.cs         # Prescription entity
└── Patient.cs              # Patient entity
```

### DTOs Layer (`src/DTOs/`)

**Shared DTOs for all API versions and adapters** — independent project with no business logic.

```
DTOs/
├── Shared/                 # Shared DTOs across all API versions
│   └── UserDto.cs          # CreateUserRequest, CreateUserResponse
│
├── V1/                     # API Version 1 DTOs
│   └── OrderDto.cs         # OrderDto, CreateOrderRequest, UpdateOrderRequest
│
└── V2/                     # API Version 2 DTOs
    └── PrescriptionOrderDto.cs  # PrescriptionOrderDto, CreatePrescriptionOrderRequest, UpdatePrescriptionOrderRequest
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
│   └── Operations/
│       ├── CreateOrder.cs      # Request + Handler in one file
│       ├── CreateOrderValidator.cs
│       ├── UpdateOrderStatus.cs
│       ├── UpdateOrderStatusValidator.cs
│       ├── CancelOrder.cs
│       ├── DeleteOrder.cs
│       ├── GetOrderById.cs
│       ├── GetAllOrders.cs
│       ├── GetOrdersByStatus.cs
│       └── GetOrdersByUser.cs
│
├── Prescriptions/              # Feature: Prescription Management
│   └── Operations/
│       ├── CreatePrescription.cs
│       ├── CreatePrescriptionValidator.cs
│       └── GetPrescriptionById.cs
│
├── Patients/                   # Feature: Patient Management
│   └── Operations/
│       ├── CreatePatient.cs
│       ├── CreatePatientValidator.cs
│       └── GetPatientById.cs
│
├── Users/                      # Feature: User Management (Admin)
│   └── Operations/
│       └── CreateApiKeyUser.cs
│
├── Behaviors/                  # MediatR Pipeline Behaviors
│   ├── LoggingBehavior.cs
│   ├── ValidationBehavior.cs
│   └── CachingBehavior.cs
│
├── Interfaces/                 # Abstractions for Infrastructure
│   ├── ICacheableQuery.cs      # Marker interface for cacheable queries
│   ├── Repositories/
│   │   ├── IRepository.cs
│   │   ├── IUnitOfWork.cs
│   │   ├── IPrescriptionOrderRepository.cs
│   │   ├── IPrescriptionRepository.cs
│   │   ├── IPatientRepository.cs
│   │   └── IUserRepository.cs
│   └── Services/
│       ├── ICacheService.cs
│       └── IApiKeyGenerator.cs
│
└── DependencyInjection.cs      # MediatR, FluentValidation registration
```

**Key Principles:**
- ✅ **Feature-based organization** - Every feature has `Operations/` folder
- ✅ **Handlers return Domain entities** - Controllers map to versioned DTOs
- ✅ **Type safety** - Domain entities use enums (e.g., `OrderStatus`)
- ✅ **External DTOs separate** - Versioned DTOs in `src/DTOs` project
- ✅ **Cross-version DTOs** - Shared DTOs in `src/DTOs/Shared/` for common types
- ✅ **UUID v7 IDs** - All entity IDs use time-sortable UUID v7

### Infrastructure Layer (`src/Infrastructure/`)

The Infrastructure layer (Interface Adapters) combines **Api** (driving adapters) and **Persistence** (driven adapters):

```
Infrastructure/
├── Api/                        # HTTP/Web adapters (driving)
│   ├── Controllers/
│   │   ├── PrescriptionsController.cs  # Shared (not versioned)
│   │   ├── V1/                 # API Version 1
│   │   │   ├── OrdersController.cs
│   │   │   ├── UsersController.cs
│   │   │   └── Mappers/
│   │   │       └── OrderMapper.cs
│   │   └── V2/                 # API Version 2
│   │       ├── OrdersController.cs
│   │       └── Mappers/
│   │           └── PrescriptionOrderMapper.cs
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
│   │   ├── CurrentUserService.cs
│   │   ├── RootAdminHealthCheck.cs
│   │   └── RootAdminInitializer.cs
│   │
│   ├── Program.cs              # Application entry point
│   └── Dockerfile
│
├── Cache/                      # Caching adapters
│   ├── CacheSettings.cs
│   ├── LocalCacheService.cs    # Local in-memory cache
│   ├── RemoteCacheService.cs   # Remote Redis cache
│   ├── NullCacheService.cs     # No-op cache for testing
│   ├── CacheActionFilter.cs    # HTTP attribute-based caching
│   └── DependencyInjection.cs
│
└── Persistence/                # Data/External adapters (driven)
    ├── Repositories/
    │   ├── MongoRepository.cs          # Generic MongoDB repository
    │   ├── MongoPrescriptionOrderRepository.cs
    │   ├── MongoPrescriptionRepository.cs
    │   ├── MongoPatientRepository.cs
    │   ├── MongoUserRepository.cs
    │   └── MongoUnitOfWork.cs
    │
    ├── Security/
    │   └── ApiKeyHasher.cs
    │
    ├── Configuration/
    │   ├── MongoDbSettings.cs
    │   ├── CorsSettings.cs
    │   ├── RateLimitSettings.cs
    │   └── RootAdminSettings.cs
    │
    └── DependencyInjection.cs

config/                         # Configuration files (separate from code)
├── appsettings.json            # Base configuration
├── appsettings.local.json      # Local development (Docker)
├── appsettings.dev.json        # Development
├── appsettings.stage.json      # Staging
├── appsettings.prod.json       # Production
├── appsettings.amr-prod.json   # AMR Production
├── appsettings.amr-stage.json  # AMR Staging
├── appsettings.eu-prod.json    # EU Production
└── appsettings.eu-stage.json   # EU Staging
```

---

## Why This Hybrid Approach?

| Layer | Organization | Reason |
|-------|--------------|--------|
| **Domain** | Entity-based | Core business objects, no dependencies (Enterprise Business Rules) |
| **Application** | Feature-based | Each use case is self-contained. Easy to find all related code. (Application Business Rules) |
| **Infrastructure** | Layer-based | Technical concerns (DB, Cache, HTTP) are shared across features. (Interface Adapters) |

---

## Navigation Tips

| I want to... | Go to... |
|--------------|----------|
| Add a new order operation | `Application/Orders/Operations/` |
| Add validation for an operation | `Application/Orders/Operations/XxxValidator.cs` |
| Change database implementation | `Infrastructure/Persistence/Repositories/` |
| Add caching logic | `Infrastructure/Cache/` |
| Add API versioned DTO | `src/DTOs/V1/` or `src/DTOs/V2/` |
| Add middleware | `Infrastructure/Api/Middleware/` |
| Configure environment | `config/appsettings.{env}.json` |

