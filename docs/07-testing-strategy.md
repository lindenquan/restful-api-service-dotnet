# Testing Strategy

## Overview

This project uses a **two-tier testing strategy**:

| Test Type | Location | Purpose |
|-----------|----------|---------|
| **Unit Tests** | `tests/Tests/` | Test business logic in isolation (including in-memory cache) |
| **API E2E Tests** | `tests/Tests.Api.E2E/` | Test API endpoints against real MongoDB + Redis |

---

## Unit Tests

### What We Test

| Layer | What's Tested | Mocked |
|-------|---------------|--------|
| **Domain** | Entity behavior, validation | Nothing |
| **Application** | Command/Query handlers, validators | Repositories |
| **Infrastructure** | Hashers, rate limiters | External services |
| **API** | Controller logic | MediatR |

### What We DO Unit Test for Cache

| Component | What's Tested |
|-----------|---------------|
| **MemoryCacheService** | L1 in-memory cache behavior (Set, Get, Remove, Exists, GetOrAdd) |
| **NullCacheService** | No-op cache behavior |

### What We DON'T Unit Test

| Component | Reason |
|-----------|--------|
| MongoDB repositories | No value testing MongoDB driver calls |
| **RedisCacheService** | **Tested in API E2E tests against real Redis** |
| **HybridCacheService** | **Tested in API E2E tests with real L1+L2** |
| EF Core DbContext | No value testing EF Core |
| HTTP client calls | No value testing HttpClient |

> **Principle**: Unit tests verify **our code** in isolation. External service integration (MongoDB, Redis) is tested via API E2E tests.

### File Structure

```
tests/Tests/
├── Domain/
│   └── Entities/
│       ├── OrderTests.cs
│       └── UserTests.cs
│
├── Application/
│   ├── Orders/
│   │   ├── CreateOrderHandlerTests.cs
│   │   ├── GetOrderByIdHandlerTests.cs
│   │   └── CreateOrderValidatorTests.cs
│   ├── Behaviors/
│   │   ├── LoggingBehaviorTests.cs
│   │   └── ValidationBehaviorTests.cs
│   └── ApiKeys/
│       └── CreateApiKeyHandlerTests.cs
│
├── Infrastructure/
│   ├── Security/
│   │   └── ApiKeyHasherTests.cs
│   └── Cache/
│       └── MemoryCacheServiceTests.cs
│
└── Api/
    └── Controllers/
        └── OrdersControllerTests.cs
```

### Running Unit Tests

```bash
# Run all unit tests
dotnet test tests/Tests

# Run with verbosity
dotnet test tests/Tests --verbosity normal

# Run specific test class
dotnet test tests/Tests --filter "FullyQualifiedName~CreateOrderHandlerTests"

# Run with coverage
dotnet test tests/Tests --collect:"XPlat Code Coverage"
```

---

## API E2E Tests

### What We Test

API E2E tests verify the **API service** against **real infrastructure** (no web client):

- ✅ HTTP → API → MongoDB flow
- ✅ Real MongoDB database
- ✅ Real Redis cache (L2)
- ✅ Hybrid cache (L1 + L2) behavior
- ✅ JSON serialization/deserialization
- ✅ Authentication flow
- ✅ Cache invalidation (Redis pub/sub)
- ✅ Concurrent operations
- ✅ Direct HTTP calls (no typed clients)

### Infrastructure

E2E tests use Docker Compose to spin up real dependencies:

```yaml
# docker-compose.e2e.yml
services:
  mongodb-e2e:
    image: mongo:7
    ports:
      - "27017:27017"

  redis-e2e:
    image: redis:7-alpine
    ports:
      - "6379:6379"
```

### File Structure

```
tests/Tests.Api.E2E/
├── Fixtures/
│   ├── ApiE2ETestFixture.cs          # WebApplicationFactory with E2E config
│   └── ApiE2ETestCollection.cs       # Shared test context
│
├── OrdersApiE2ETests.cs              # CRUD operations via HttpClient
├── RedisCacheE2ETests.cs             # Redis cache integration tests
└── AdminApiE2ETests.cs               # Admin operations (if needed)
```

### Running API E2E Tests

```bash
# 1. Start MongoDB and Redis
./build.ps1 docker-up

# 2. Wait for healthy containers
docker compose ps

# 3. Run API E2E tests
dotnet test tests/Tests.Api.E2E

# Or use build.ps1 (handles Docker automatically for local)
./build.ps1 test-api-e2e

# 4. Run specific test class
dotnet test tests/Tests.Api.E2E --filter "FullyQualifiedName~OrdersApiE2ETests"

# 5. Stop dependencies
./build.ps1 docker-down
```

### API E2E Test Example

```csharp
[Collection(nameof(ApiE2ETestCollection))]
public sealed class OrdersApiE2ETests : IAsyncLifetime
{
    private readonly ApiE2ETestFixture _fixture;
    private readonly List<string> _createdOrderIds = new();

    [Fact]
    public async Task CreateOrder_WithValidData_ShouldReturn201()
    {
        // Arrange
        var request = new CreateOrderRequest(
            PatientId: 1,
            PrescriptionId: 1,
            Notes: "E2E test order"
        );

        // Act - Direct HTTP call (no typed client)
        var response = await _fixture.Client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order.Should().NotBeNull();
        order!.Status.Should().Be("Pending");

        _createdOrderIds.Add(order.Id);
    }

    public async Task DisposeAsync()
    {
        // Clean up created orders
        foreach (var orderId in _createdOrderIds)
        {
            await _fixture.Client.DeleteAsync($"/api/orders/{orderId}");
        }
    }
}
```

### Redis Cache E2E Test Example

```csharp
[Fact]
public async Task UpdateOrder_ShouldInvalidateCache()
{
    // Arrange - create and cache an order
    var createRequest = new CreateOrderRequest(
        PatientId: 1,
        PrescriptionId: 1,
        Notes: "Cache invalidation test"
    );
    var createResponse = await _fixture.Client.PostAsJsonAsync("/api/orders", createRequest);
    var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

    // Get the order (should be cached in Redis)
    var getResponse1 = await _fixture.Client.GetAsync($"/api/orders/{createdOrder!.Id}");
    var order1 = await getResponse1.Content.ReadFromJsonAsync<OrderResponse>();
    order1!.Status.Should().Be("Pending");

    // Act - update the order (should invalidate Redis cache)
    var updateRequest = new UpdateOrderRequest(Status: "Processing");
    await _fixture.Client.PutAsJsonAsync($"/api/orders/{createdOrder.Id}", updateRequest);

    // Get the order again (should reflect the update, not cached value)
    var getResponse2 = await _fixture.Client.GetAsync($"/api/orders/{createdOrder.Id}");
    var order2 = await getResponse2.Content.ReadFromJsonAsync<OrderResponse>();

    // Assert - should reflect the update (cache was invalidated)
    order2!.Status.Should().Be("Processing");
}
```

---

## Test Coverage Goals

| Layer | Target Coverage | Notes |
|-------|-----------------|-------|
| Domain | 100% | Pure business logic |
| Application Handlers | 90%+ | Core use cases |
| Application Validators | 100% | All validation rules |
| Infrastructure (Cache - L1) | 90%+ | MemoryCacheService unit tests |
| Infrastructure (Cache - L2) | 0% | RedisCacheService covered by API E2E |
| Infrastructure (DB) | 0% | MongoDB covered by API E2E |
| API Controllers | 70%+ | Request/response mapping |

---

## Testing Pyramid

```
        ┌───────────┐
        │  API E2E  │  ← Few, slow, expensive
        │   Tests   │     Test API → MongoDB + Redis
        ├───────────┤     Direct HTTP calls
        │           │
        │   Unit    │  ← Many, fast, cheap
        │   Tests   │     Test business logic + L1 cache
        │           │
        └───────────┘
```



---

## Mocking Strategy

### Tools Used

- **Moq** — Mocking framework
- **FluentAssertions** — Assertion library
- **xUnit** — Test framework

### Example: Mocking Repository

```csharp
public class CreateOrderHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateOrderHandler _handler;
    
    public CreateOrderHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateOrderHandler(_unitOfWorkMock.Object);
    }
    
    [Fact]
    public async Task Handle_ValidCommand_ReturnsOrderId()
    {
        // Arrange
        _unitOfWorkMock
            .Setup(u => u.Orders.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "Notes");
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.Should().NotBeEmpty();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

