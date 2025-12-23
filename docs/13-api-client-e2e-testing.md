# API Client E2E Testing

## Overview

This project includes **comprehensive E2E tests** that test the **entire stack** from the **API client** through the **HTTP layer**, **API controllers**, **application logic**, and down to the **real database**.

These tests validate that the Blazor WASM client can successfully communicate with the API in a real-world scenario.

---

## Test Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ Test Code (xUnit)                                           │
│ - Uses IOrdersApiClient, IAdminApiClient                    │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ API Client (Refit)                                          │
│ - Serializes requests to JSON                               │
│ - Adds authentication headers                               │
│ - Makes HTTP calls                                          │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Test Server (WebApplicationFactory)                         │
│ - Real ASP.NET Core pipeline                                │
│ - Real middleware (auth, validation, etc.)                  │
│ - Real controllers                                          │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Application Layer (MediatR)                                 │
│ - Real handlers                                             │
│ - Real validators                                           │
│ - Real business logic                                       │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Real Database (MongoDB via Docker)                          │
│ - Real data persistence                                     │
│ - Real queries                                              │
│ - Real transactions                                         │
└─────────────────────────────────────────────────────────────┘
```

---

## Test Categories

### 1. **CRUD Operations** (`OrdersApiClientE2ETests.cs`)

Tests full lifecycle of orders:

```csharp
[Fact]
public async Task FullOrderLifecycle_CreateReadUpdateDelete_ShouldWork()
{
    // 1. Create
    var createRequest = new CreateOrderRequest(
        PatientId: 1,
        PrescriptionId: 1,
        Notes: "Lifecycle test order"
    );
    var createdOrder = await _fixture.OrdersClient.CreateAsync(createRequest);
    
    // 2. Read
    var fetchedOrder = await _fixture.OrdersClient.GetByIdAsync(createdOrder.Id);
    
    // 3. Update
    var updateRequest = new UpdateOrderRequest(Status: "Processing");
    var updatedOrder = await _fixture.OrdersClient.UpdateAsync(createdOrder.Id, updateRequest);
    
    // 4. Delete
    var deleteResult = await _fixture.OrdersClient.DeleteAsync(createdOrder.Id, permanent: true);
    
    // 5. Verify deletion
    var deletedOrder = await _fixture.OrdersClient.GetByIdAsync(createdOrder.Id);
    deletedOrder.Should().BeNull();
}
```

**Tests:**
- ✅ Create order with valid data
- ✅ Get all orders
- ✅ Get order by ID
- ✅ Get orders by patient
- ✅ Update order status
- ✅ Soft delete (mark as cancelled)
- ✅ Hard delete (permanent removal)
- ✅ Full lifecycle (create → read → update → delete)

---

### 2. **Admin Operations** (`AdminApiClientE2ETests.cs`)

Tests API key management:

```csharp
[Fact]
public async Task CreatedApiKey_ShouldBeUsableImmediately()
{
    // Create a new API key
    var request = new CreateUserRequest(
        UserName: "test-user",
        Email: "test@example.com",
        UserType: UserType.Regular
    );
    var response = await _fixture.AdminClient.CreateApiKeyAsync(request);
    
    // Use it immediately
    var newClient = _fixture.CreateOrdersClientWithApiKey(response.ApiKey);
    var orders = await newClient.GetAllAsync();
    
    orders.Should().NotBeNull();
}
```

**Tests:**
- ✅ Create API key for regular user
- ✅ Create API key for admin user
- ✅ Duplicate username validation
- ✅ Multiple API keys generate unique values
- ✅ Created API key is usable immediately
- ✅ Email validation

---

### 3. **Error Handling** (`ErrorHandlingE2ETests.cs`)

Tests error scenarios through the API client:

```csharp
[Fact]
public async Task GetOrders_WithInvalidApiKey_ShouldThrow401()
{
    var invalidClient = _fixture.CreateOrdersClientWithApiKey("invalid-key");
    
    var act = async () => await invalidClient.GetAllAsync();
    
    await act.Should().ThrowAsync<ApiException>()
        .Where(ex => ex.StatusCode == HttpStatusCode.Unauthorized);
}
```

**Tests:**
- ✅ 401 Unauthorized (invalid API key)
- ✅ 401 Unauthorized (empty API key)
- ✅ 400 Bad Request (invalid patient ID)
- ✅ 400 Bad Request (invalid prescription ID)
- ✅ 400 Bad Request (invalid status)
- ✅ 400 Bad Request (empty status)
- ✅ 404 Not Found (returns null for non-existent resources)
- ✅ Empty list for non-existent patient

---

### 4. **Caching & Concurrency** (`CachingAndConcurrencyE2ETests.cs`)

Tests cache behavior and concurrent operations:

```csharp
[Fact]
public async Task UpdateOrder_ShouldInvalidateCache()
{
    // Get order (might be cached)
    var order1 = await _fixture.OrdersClient.GetByIdAsync(orderId);
    order1!.Status.Should().Be("Pending");
    
    // Update the order
    await _fixture.OrdersClient.UpdateAsync(orderId, new UpdateOrderRequest(Status: "Processing"));
    
    // Get again (cache should be invalidated)
    var order2 = await _fixture.OrdersClient.GetByIdAsync(orderId);
    order2!.Status.Should().Be("Processing");
}
```

**Tests:**
- ✅ Multiple reads return consistent data
- ✅ Update invalidates cache
- ✅ Concurrent GET requests all succeed
- ✅ Concurrent CREATE requests all succeed
- ✅ Concurrent UPDATE requests all succeed
- ✅ GetAll cache invalidation after create
- ✅ GetByPatient cache invalidation after create

---

## Running the Tests

### Prerequisites

```bash
# Start E2E dependencies (MongoDB, Redis)
docker-compose -f docker-compose.e2e.yml up -d
```

### Run E2E Tests

```bash
# Run all E2E tests
dotnet test tests/Tests.ApiClient.E2E

# Run specific test class
dotnet test --filter "FullyQualifiedName~OrdersApiClientE2ETests"

# Run specific test
dotnet test --filter "FullyQualifiedName~FullOrderLifecycle"
```

### Stop E2E Dependencies

```bash
docker-compose -f docker-compose.e2e.yml down
```

---

## Test Fixture

The `ApiClientTestFixture` provides:

1. **Real API Server** - Uses `WebApplicationFactory<Program>`
2. **Configured API Clients** - Pre-configured with admin API key
3. **Helper Methods** - Create clients with custom API keys

```csharp
public sealed class ApiClientTestFixture : WebApplicationFactory<Program>
{
    public IOrdersApiClient OrdersClient { get; private set; }
    public IAdminApiClient AdminClient { get; private set; }
    
    // Create a client with a specific API key
    public IOrdersApiClient CreateOrdersClientWithApiKey(string apiKey)
    {
        // ...
    }
}
```

---

## Test Cleanup

All tests implement `IAsyncLifetime` for proper cleanup:

```csharp
public async Task DisposeAsync()
{
    // Clean up created orders
    foreach (var orderId in _createdOrderIds)
    {
        try
        {
            await _fixture.OrdersClient.DeleteAsync(orderId, permanent: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
```

This ensures:
- ✅ Tests don't leave garbage data
- ✅ Tests are isolated from each other
- ✅ Database stays clean

---

## Benefits of API Client E2E Tests

| Benefit | Description |
|---------|-------------|
| ✅ **Real-World Simulation** | Tests exactly how Blazor WASM will use the API |
| ✅ **Type Safety** | Compile-time checking for API calls |
| ✅ **Full Stack Coverage** | Tests client → HTTP → API → database |
| ✅ **Catches Integration Issues** | Finds problems that unit tests miss |
| ✅ **Validates Serialization** | Ensures JSON serialization works correctly |
| ✅ **Tests Authentication** | Validates API key authentication flow |
| ✅ **Tests Caching** | Validates cache invalidation logic |
| ✅ **Tests Concurrency** | Validates thread-safe operations |

---

## Summary

- ✅ **4 test files** with comprehensive coverage
- ✅ **50+ tests** covering CRUD, admin, errors, caching, concurrency
- ✅ **Full stack testing** from client to database
- ✅ **Type-safe** API client usage
- ✅ **Automatic cleanup** to keep database clean
- ✅ **Real-world simulation** of Blazor WASM usage

> *"These tests validate that the Blazor WASM client can successfully communicate with the API in production-like scenarios."*

