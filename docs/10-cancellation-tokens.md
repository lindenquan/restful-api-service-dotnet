# CancellationToken Best Practices

## Overview

`CancellationToken` is a **cooperative cancellation mechanism** in .NET that allows graceful termination of async operations. It's essential for building responsive, resource-efficient APIs.

---

## What Problem Does It Solve?

**Scenario:**
- User makes HTTP request → Server starts processing
- User closes browser after 2 seconds
- **Without CancellationToken**: Server wastes resources for 30+ seconds
- **With CancellationToken**: Operation stops immediately, resources freed

---

## How It Works

### Basic Flow

```csharp
public async Task<Order> GetOrderAsync(int id, CancellationToken ct)
{
    // 1. Check if cancellation was requested
    ct.ThrowIfCancellationRequested();
    
    // 2. Pass token to async operations
    var order = await _db.Orders.FindAsync(id, ct);
    
    // 3. Check again after long operations
    ct.ThrowIfCancellationRequested();
    
    return order;
}
```

**When Cancelled:**
1. `ct.IsCancellationRequested` becomes `true`
2. `ct.ThrowIfCancellationRequested()` throws `OperationCanceledException`
3. Operation stops gracefully
4. Resources are freed

---

## Common Use Cases

### 1. HTTP Request Cancellation (Automatic)

ASP.NET Core automatically provides a `CancellationToken` that's cancelled when:
- User closes browser
- Request times out
- Connection is lost
- Client navigates away

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct)
{
    // ASP.NET Core automatically binds CancellationToken
    // It's cancelled if user closes browser or connection drops
    
    var order = await _mediator.Send(new GetOrderByIdQuery(id), ct);
    return Ok(order);
}
```

### 2. Manual Cancellation with Timeout

```csharp
// Create a cancellation token source with 30-second timeout
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var result = await ProcessLargeFileAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation timed out after 30 seconds");
}
```

### 3. User-Initiated Cancellation

```csharp
var cts = new CancellationTokenSource();

// Start long-running task
var task = ProcessDataAsync(cts.Token);

// User clicks "Cancel" button
cancelButton.Click += (s, e) => cts.Cancel();
```

---

## Implementation in This Project

### Handler Example

```csharp
// src/Application/Orders/Operations/GetOrderById.cs
public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, InternalOrderDto?>
{
    public async Task<InternalOrderDto?> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        // Pass token to database operation
        var order = await _unitOfWork.PrescriptionOrders
            .GetByIdWithDetailsAsync(request.OrderId, ct);
        
        return order == null ? null : EntityToInternalDto.Map(order);
    }
}
```

### Controller Example

```csharp
// src/Adapters/Api/Controllers/V1/OrdersController.cs
[HttpGet("{id}")]
public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct)
{
    var internalDto = await _mediator.Send(new GetOrderByIdQuery(id), ct);
    if (internalDto == null) return NotFound();
    
    return Ok(OrderMapper.ToV1Dto(internalDto));
}
```

**Flow:**
```
HTTP Request → Controller (ct) → MediatR (ct) → Handler (ct) → Repository (ct) → Database
                                                                                      ↓
User closes browser → ct.IsCancellationRequested = true → Database query stops
```

---

## Best Practices

### ✅ DO

```csharp
// 1. Accept CancellationToken in all async methods
public async Task<Order> GetOrderAsync(int id, CancellationToken ct)

// 2. Pass it to ALL async operations
var order = await _db.Orders.FindAsync(id, ct);
await _cache.SetAsync(key, value, ct);
await _emailService.SendAsync(email, ct);

// 3. Check periodically in long loops
foreach (var item in largeList)
{
    ct.ThrowIfCancellationRequested();
    await ProcessItemAsync(item, ct);
}

// 4. Make it optional with default value
public async Task DoWorkAsync(CancellationToken ct = default)
{
    // Can be called with or without a token
}
```

### ❌ DON'T

```csharp
// ❌ Don't ignore the token parameter
public async Task<Order> GetOrderAsync(int id, CancellationToken ct)
{
    var order = await _db.Orders.FindAsync(id);  // ← Should pass ct!
    return order;
}

// ❌ Don't catch and swallow OperationCanceledException
try
{
    await DoWorkAsync(ct);
}
catch (OperationCanceledException)
{
    // ❌ Don't suppress - let it propagate to framework!
}

// ❌ Don't create new tokens unnecessarily
public async Task DoWorkAsync(CancellationToken ct)
{
    var newCt = new CancellationTokenSource().Token;  // ❌ Use the provided ct!
    await _db.SaveAsync(newCt);
}
```

---

## Benefits

| Benefit | Description |
|---------|-------------|
| **Resource Efficiency** | Stop wasting CPU/memory on abandoned requests |
| **Better UX** | Faster response when users navigate away |
| **Database Load** | Reduce unnecessary database queries |
| **Scalability** | Handle more concurrent users with same resources |
| **Graceful Shutdown** | Clean application shutdown without orphaned operations |

---

## Testing with CancellationToken

```csharp
[Fact]
public async Task Handle_WithCancelledToken_ShouldThrowOperationCanceledException()
{
    // Arrange
    var cts = new CancellationTokenSource();
    cts.Cancel();  // Cancel immediately
    
    var handler = new GetOrderByIdHandler(_unitOfWork);
    var query = new GetOrderByIdQuery(1);
    
    // Act & Assert
    await Assert.ThrowsAsync<OperationCanceledException>(
        () => handler.Handle(query, cts.Token)
    );
}
```

---

## Summary

- ✅ **Always accept** `CancellationToken` in async methods
- ✅ **Always pass** it to async operations (database, cache, HTTP calls)
- ✅ **Check periodically** in long-running loops
- ✅ **Let exceptions propagate** - don't catch `OperationCanceledException`
- ✅ **Use default parameter** to make it optional: `CancellationToken ct = default`

> **Rule of Thumb:** If a method is `async`, it should accept a `CancellationToken`.

