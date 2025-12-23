# Testing Strategy

## Overview

This project uses a **two-tier testing strategy**:

| Test Type | Location | Purpose |
|-----------|----------|---------|
| **Unit Tests** | `tests/Tests/` | Test business logic in isolation |
| **E2E Tests** | `tests/Tests.E2E/` | Test full system with real dependencies |

---

## Unit Tests

### What We Test

| Layer | What's Tested | Mocked |
|-------|---------------|--------|
| **Domain** | Entity behavior, validation | Nothing |
| **Application** | Command/Query handlers, validators | Repositories |
| **Infrastructure** | Hashers, rate limiters | External services |
| **API** | Controller logic | MediatR |

### What We DON'T Unit Test

| Component | Reason |
|-----------|--------|
| MongoDB repositories | No value testing MongoDB driver calls |
| Redis cache service | No value testing Redis client calls |
| EF Core DbContext | No value testing EF Core |
| HTTP client calls | No value testing HttpClient |

> **Principle**: Unit tests verify **our code**, not third-party libraries. External service integration is tested via E2E tests.

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
│   └── Security/
│       └── ApiKeyHasherTests.cs
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

## E2E Tests

### What We Test

E2E tests verify the **complete system** including:

- Full HTTP request/response cycle
- Real MongoDB database
- Real Redis cache
- Authentication flow
- Authorization rules

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
tests/Tests.E2E/
├── Fixtures/
│   ├── ApiWebApplicationFactory.cs   # Custom test server
│   └── E2ETestCollection.cs          # Shared test context
│
├── OrdersE2ETests.cs                 # CRUD operations
└── AdminE2ETests.cs                  # Admin endpoints
```

### Running E2E Tests

```bash
# 1. Start dependencies
docker-compose -f docker-compose.e2e.yml up -d

# 2. Wait for healthy containers
docker-compose -f docker-compose.e2e.yml ps

# 3. Run E2E tests
dotnet test tests/Tests.E2E

# 4. Stop dependencies
docker-compose -f docker-compose.e2e.yml down -v
```

### E2E Test Example

```csharp
public class OrdersE2ETests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    
    public OrdersE2ETests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", "test-api-key");
    }
    
    [Fact]
    public async Task CreateOrder_ReturnsCreated()
    {
        // Arrange
        var request = new { UserId = Guid.NewGuid(), PrescriptionId = Guid.NewGuid() };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/orders", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

---

## Test Coverage Goals

| Layer | Target Coverage | Notes |
|-------|-----------------|-------|
| Domain | 100% | Pure business logic |
| Application Handlers | 90%+ | Core use cases |
| Application Validators | 100% | All validation rules |
| Infrastructure (non-DB) | 80%+ | Hashers, utilities |
| Infrastructure (DB) | 0% | Covered by E2E |
| API Controllers | 70%+ | Request/response mapping |

---

## Testing Pyramid

```
        ┌───────────┐
        │    E2E    │  ← Few, slow, expensive
        │   Tests   │     Test full system
        ├───────────┤
        │           │
        │Integration│  ← Some, medium speed
        │   Tests   │     (Currently merged with E2E)
        │           │
        ├───────────┤
        │           │
        │   Unit    │  ← Many, fast, cheap
        │   Tests   │     Test business logic
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

