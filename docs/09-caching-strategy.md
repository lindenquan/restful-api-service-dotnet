# Caching Strategy

This document describes the caching architecture with two complementary approaches: Application Layer (MediatR pipeline) and Infrastructure Layer (HTTP attributes).

## Overview

The API provides **two caching approaches** that work together:

| Approach | Layer | Mechanism | Use Case |
|----------|-------|-----------|----------|
| **Application Layer** | MediatR Pipeline | `ICacheableQuery`, `ICacheInvalidatingCommand` | Commands/Queries with transaction coordination |
| **Infrastructure Layer** | HTTP Attributes | `[LocalCache]`, `[RemoteCache]` | Controller-level response caching |

Both approaches use the same underlying cache services:
- **Local Cache**: In-memory cache for static reference data (infinite TTL)
- **Remote Cache**: Redis-based distributed cache with lock-based consistency

> ⚠️ **Important**: Local and Remote caches serve different purposes:
> - **Local**: Use ONLY for static reference data that never changes (e.g., drug lists, ICD codes)
> - **Remote**: Use for dynamic data with configurable consistency (Eventual, Strong, Serializable)

## Cache Configuration

```json
{
  "Cache": {
    "Local": {
      "Enabled": true,
      "MaxItems": 10000
    },
    "Remote": {
      "Enabled": true,
      "ConnectionString": "redis:6379",
      "InstanceName": "PrescriptionApi:",
      "TtlSeconds": 300,
      "ConnectTimeout": 5000,
      "SyncTimeout": 1000,
      "LockTimeoutSeconds": 5,
      "LockWaitTimeoutMs": 1000,
      "LockRetryDelayMs": 50
    }
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `Local.Enabled` | Enable in-memory static data cache | false |
| `Local.MaxItems` | Maximum items in Local cache (LRU eviction) | 10000 |
| `Remote.Enabled` | Enable Redis cache | false |
| `Remote.ConnectionString` | Redis connection string | localhost:6379 |
| `Remote.TtlSeconds` | Cache TTL in seconds (0 = infinite) | 300 |
| `Remote.InstanceName` | Redis key prefix | PrescriptionApi: |
| `Remote.ConnectTimeout` | Redis connection timeout (ms) | 5000 |
| `Remote.SyncTimeout` | Redis operation timeout (ms) | 1000 |
| `Remote.LockTimeoutSeconds` | Lock auto-expiry for safety (seconds) | 5 |
| `Remote.LockWaitTimeoutMs` | Max wait for lock (Serializable mode) | 1000 |
| `Remote.LockRetryDelayMs` | Retry delay when waiting for lock | 50 |

## Local Cache: Static Data Cache

Local cache is a **simple in-memory cache with infinite TTL** designed for static reference data.

### Local Cache Characteristics

| Aspect | Behavior |
|--------|----------|
| **TTL** | Infinite (never expires) |
| **Invalidation** | None - entries are NOT invalidated on writes |
| **Eviction** | Only when `MaxItems` limit reached (LRU) or app restart |
| **Scope** | Local to each instance (not shared) |

### When to Use Local Cache

| Data Type | Use Local? | Example |
|-----------|------------|---------|
| Static reference data | ✅ Yes | Drug lists, ICD codes, units of measure |
| Configuration | ✅ Yes | Feature flags loaded at startup |
| Lookup tables | ✅ Yes | Country codes, status enums |
| User/patient data | ❌ No | Changes frequently |
| Transactional data | ❌ No | Must always be fresh |

> ⚠️ **Warning**: Do NOT use Local cache for data that can be updated or deleted. Entries persist until app restart or LRU eviction.

## Remote Cache: Distributed Cache with Lock-Based Consistency

Remote cache is a **Redis-based distributed cache** with lock-based consistency modes.

> ⚠️ **Limitation: Single Redis Instance Only**
>
> This application supports only a **single Redis instance** for remote caching. It will **NOT work correctly** with multiple Redis instances or Redis Cluster configurations.
>
> **Why?** The lock-based consistency (Strong/Serializable modes) relies on Redis locks that must be visible to all API instances. With multiple independent Redis instances:
> - Locks acquired on one Redis instance are invisible to others
> - Cache invalidation on one instance doesn't propagate to others
> - This leads to stale reads and lost updates
>
> **Supported configurations:**
> - ✅ Single Redis instance
> - ✅ Redis with replicas (read replicas for HA, single primary for writes)
> - ✅ Redis Sentinel (automatic failover to single primary)
> - ❌ Multiple independent Redis instances
> - ❌ Redis Cluster with hash slots (locks don't span slots)
> - ❌ Client-side sharding across Redis instances

### Consistency Modes

| Mode | Read Behavior During Write | Stale Data Window | Use Case |
|------|---------------------------|-------------------|----------|
| **Eventual** | Read stale cache | Up to TTL | High-read, low-write data |
| **Strong** | Bypass cache, read from DB | 0 (during write) | Most write operations |
| **Serializable** | Wait for write to complete | 0 (strict ordering) | Critical operations |

### Eventual Consistency

**Mechanism**: TTL-based expiration only, no locking

- Cache entries expire after configured `TtlSeconds`
- Writes do not affect concurrent reads
- Simplest and fastest mode

**Stale Data Window**: Up to configured `TtlSeconds`

**Best for**: High-read, low-write data where brief staleness is acceptable

### Strong Consistency

**Mechanism**: Lock on write, readers bypass cache when locked

- Writer acquires lock before updating data
- Concurrent readers see the lock and bypass cache, reading directly from DB
- Lock is released after write completes

**Stale Data Window**: 0 during writes (readers go to DB)

**Best for**: Most write operations where consistency matters

### Serializable Consistency

**Mechanism**: Lock on write, readers wait for lock release

- Writer acquires lock before updating data
- Concurrent readers wait for lock to be released (up to `LockWaitTimeoutMs`)
- After lock release, readers get fresh data from cache

**Stale Data Window**: 0 (strict read/write ordering)

**Best for**: Critical operations requiring strict ordering (e.g., account balance queries)

### When to Use Each Consistency Level

| Consistency | Use When | Example Endpoints |
|-------------|----------|-------------------|
| **Eventual** | Staleness is acceptable for performance | Product catalogs, news feeds, reports |
| **Strong** | Need fresh data, higher DB load OK during writes | Patient records, user profiles |
| **Serializable** | Must see latest data, willing to wait | Account balances, inventory counts |

#### Decision Guide

```
┌─────────────────────────────────────────────────────────────────┐
│              Is brief staleness acceptable?                      │
└──────────────────────────┬──────────────────────────────────────┘
                           │
            Yes ───────────┼─────────── No
             │             │             │
             ▼             │             ▼
      ┌───────────┐        │    ┌───────────────────────────────┐
      │ Eventual  │        │    │ During writes, is higher DB   │
      │ (fastest) │        │    │ load acceptable?              │
      └───────────┘        │    └───────────────┬───────────────┘
                           │                    │
                           │     Yes ───────────┼─────────── No
                           │      │             │             │
                           │      ▼             │             ▼
                           │ ┌─────────┐        │      ┌──────────────┐
                           │ │ Strong  │        │      │ Serializable │
                           │ │ (bypass)│        │      │ (wait)       │
                           │ └─────────┘        │      └──────────────┘
```

#### Detailed Comparison

| Aspect | Eventual | Strong | Serializable |
|--------|----------|--------|--------------|
| **Read during write** | Returns stale cache | Bypasses to DB | Waits for write |
| **Read latency (normal)** | ~1ms | ~1ms | ~1ms |
| **Read latency (during write)** | ~1ms | 5-50ms (DB) | Blocked until write done |
| **DB load during writes** | Low | High (all readers hit DB) | Low (readers wait) |
| **Dirty reads possible** | Yes (up to TTL) | No | No |
| **Read ordering guaranteed** | No | No | Yes |

#### Examples by Domain

```csharp
// E-commerce product catalog - staleness OK for a few seconds
[HttpGet("products")]
[RemoteCache(CacheConsistency.Eventual)]
public async Task<ActionResult> GetProducts() { ... }

// Patient medical record - must not be stale
[HttpGet("patients/{id}")]
[RemoteCache(CacheConsistency.Strong)]
public async Task<ActionResult> GetPatient(int id) { ... }

// Account balance - must see exact current value
[HttpGet("accounts/{id}/balance")]
[RemoteCache(CacheConsistency.Serializable)]
public async Task<ActionResult> GetBalance(int id) { ... }
```

### Cache Consistency vs Database Isolation

> ⚠️ **Important**: Cache consistency and database transaction isolation solve **different problems**. For critical operations (financial, inventory), you need **BOTH**.

#### Two Different Problems

| Problem | Scenario | Consequence | Solution |
|---------|----------|-------------|----------|
| **Lost updates** | Two writers modify same record | One write is silently lost | MongoDB Serializable isolation |
| **Stale reads** | Reader gets cached value during write | Reader sees outdated data | Cache Strong/Serializable |

#### Why You Need Both Layers

**Scenario: MongoDB Serializable + Cache Eventual (WRONG)**

```
Timeline:
───────────────────────────────────────────────────────────────────
Writer A                             Reader B (GET balance)
───────────────────────────────────────────────────────────────────
Cache: balance = 100
DB: balance = 100

1. Begin transaction (Serializable)
2. Update DB: balance = 50
3. Commit transaction ✓
                                     4. GET /accounts/123/balance
                                     5. Cache HIT → Returns 100 ← STALE!
6. Invalidate cache (too late)
───────────────────────────────────────────────────────────────────
Result: Reader B sees stale data even though DB is correct
```

**Scenario: MongoDB Serializable + Cache Strong (CORRECT)**

```
Timeline:
───────────────────────────────────────────────────────────────────
Writer A                             Reader B (GET balance)
───────────────────────────────────────────────────────────────────
Cache: balance = 100
DB: balance = 100

1. Lock cache key
2. Begin transaction (Serializable)
3. Update DB: balance = 50
                                     4. GET /accounts/123/balance
                                     5. Cache LOCKED → Bypass to DB
                                     6. Read DB: balance = 50 ✓
4. Commit transaction ✓
5. Invalidate cache
6. Release lock
───────────────────────────────────────────────────────────────────
Result: Reader B sees correct data (50)
```

#### Configuration Matrix

| MongoDB Isolation | Cache Consistency | Lost Updates | Stale Reads | Use Case |
|-------------------|-------------------|--------------|-------------|----------|
| None | Eventual | ❌ Possible | ❌ Possible | Non-critical, high-perf |
| Snapshot | Eventual | ❌ Possible | ❌ Possible | Read-heavy, staleness OK |
| Snapshot | Strong | ❌ Possible | ✅ Protected | Most CRUD operations |
| Serializable | Eventual | ✅ Protected | ❌ Possible | Write-heavy, reads less critical |
| **Serializable** | **Strong** | ✅ Protected | ✅ Protected | **Financial, inventory** |
| **Serializable** | **Serializable** | ✅ Protected | ✅ Protected | **Banking, strict ordering** |

#### Recommended Configuration for Critical Operations

```json
{
  "MongoDB": {
    "Transaction": {
      "IsolationLevel": "Serializable",
      "MaxCommitTimeSeconds": 120,
      "RetryWrites": true
    }
  },
  "Cache": {
    "Remote": {
      "Enabled": true,
      "Consistency": "Strong",
      "LockTimeoutMs": 5000,
      "LockWaitTimeoutMs": 3000
    }
  }
}
```

#### Per-Endpoint Override

For endpoints requiring strictest consistency:

```csharp
// Account balance - both DB and cache protection
[HttpGet("accounts/{id}/balance")]
[RemoteCache(CacheConsistency.Serializable)]
public async Task<ActionResult> GetBalance(int id)
{
    return Ok(await _accountService.GetBalanceAsync(id));
}

[HttpPost("accounts/{id}/deduct")]
[RemoteCache(CacheConsistency.Strong, InvalidateKeys = ["accounts:{id}:*"])]
public async Task<ActionResult> DeductBalance(int id, DeductRequest request)
{
    await _unitOfWork.BeginTransactionAsync(); // Uses Serializable from config
    try
    {
        await _accountService.DeductAsync(id, request.Amount);
        await _unitOfWork.CommitTransactionAsync();
        return Ok();
    }
    catch
    {
        await _unitOfWork.RollbackTransactionAsync();
        throw;
    }
}
```

#### Summary: Which Layer Protects What

| Layer | Protects Against | Without It |
|-------|------------------|------------|
| **MongoDB Serializable** | Writers overwriting each other | Lost updates (money disappears) |
| **Cache Strong** | Readers getting stale cache during writes | Dirty reads (wrong balance shown) |
| **Cache Serializable** | Same as Strong + strict read ordering | Readers may see slightly stale data |

**For financial/critical operations: Use MongoDB Serializable + Cache Strong/Serializable**

See [MongoDB documentation](https://www.mongodb.com/docs/manual/core/read-isolation-consistency-recency/) for database-level isolation details.

### Disabling Cache for Perfect Consistency

**If your use case requires data to never be stale, disable caching entirely:**

```json
{
  "Cache": {
    "Local": { "Enabled": false },
    "Remote": { "Enabled": false }
  }
}
```

This ensures every request reads directly from MongoDB. Trade-off: Higher latency and database load.

## Performance Characteristics

### Read Latency (Cache Hit)

| Configuration | Read Latency | Speedup vs No Cache | Notes |
|---------------|--------------|---------------------|-------|
| **No Cache** | 5-50 ms | 1x (baseline) | Every read hits MongoDB |
| **Remote Only** | 0.5-2 ms | ~10-50x faster | Redis network round-trip |
| **Local Only** | 0.001-0.01 ms | ~1000-5000x faster | In-process memory access |

### Write Overhead

| Consistency Mode | Write Overhead | Description |
|------------------|----------------|-------------|
| **No Cache** | 0 ms | No cache to update |
| **Eventual** | +0.5-2 ms | Write to Redis + invalidate |
| **Strong** | +1-3 ms | Lock + write + invalidate + unlock |
| **Serializable** | +1-5 ms | Lock + write + invalidate + unlock (readers wait) |

### Throughput Estimates (Single Instance)

| Configuration | Estimated RPS | Notes |
|---------------|---------------|-------|
| No Cache | 100-500 | MongoDB bottleneck |
| Remote Only | 1,000-5,000 | Redis bottleneck |
| Local Only | 10,000-50,000 | Memory + GC pressure |

> **Note**: Actual throughput depends on query complexity, data size, hardware, and concurrent connections.

### Choosing the Right Configuration

```
                    ┌─────────────────────────────────────┐
                    │   Is the data static/reference?     │
                    └──────────────────┬──────────────────┘
                                       │
                         Yes ──────────┼────────── No
                          │            │            │
                          ▼            │            ▼
                    ┌─────────────┐    │    ┌─────────────────────────┐
                    │ Use Local   │    │    │ How critical is         │
                    │ [LocalCache]│    │    │ consistency?            │
                    └─────────────┘    │    └────────────┬────────────┘
                                       │                 │
                                       │   Low ──────────┼────── High
                                       │    │            │        │
                                       │    ▼            │        ▼
                                       │ ┌──────────────┐│  ┌──────────────┐
                                       │ │ Eventual     ││  │ Strong or    │
                                       │ │ (fastest)    ││  │ Serializable │
                                       │ └──────────────┘│  └──────────────┘
```

## Attribute-Based Caching

Cache is applied per-endpoint via attributes on controller actions.

### Using Local Cache (Static Data)

Apply `[LocalCache]` to endpoints returning static reference data:

```csharp
[HttpGet("drugs")]
[LocalCache("drugs:all")]
public async Task<IActionResult> GetAllDrugs()
{
    // Data is cached in-memory with infinite TTL
    return Ok(await _drugService.GetAllAsync());
}
### Using Remote Cache (Dynamic Data)

Apply `[RemoteCache]` to endpoints returning dynamic data:

```csharp
[HttpGet("{id}")]
[RemoteCache("patient:{id}", Consistency = CacheConsistency.Strong)]
public async Task<IActionResult> GetPatient(string id)
{
    // Data is cached in Redis with Strong consistency
    return Ok(await _patientService.GetByIdAsync(id));
}

[HttpPut("{id}")]
[RemoteCacheInvalidate("patient:{id}", Consistency = CacheConsistency.Strong)]
public async Task<IActionResult> UpdatePatient(string id, UpdatePatientDto dto)
{
    // Cache is invalidated with lock-based consistency
    return Ok(await _patientService.UpdateAsync(id, dto));
}
```

### Consistency Mode Selection

```csharp
// Eventual - fastest, allows stale reads during writes
[RemoteCache("data:{id}", Consistency = CacheConsistency.Eventual)]

// Strong - readers bypass cache during writes
[RemoteCache("data:{id}", Consistency = CacheConsistency.Strong)]

// Serializable - readers wait for writes to complete
[RemoteCache("data:{id}", Consistency = CacheConsistency.Serializable)]
```

## Application Layer Cache Coordination

The Application layer coordinates cache operations with database transactions through the **MediatR pipeline**. This ensures cache invalidation only occurs after successful database operations.

### Why Application Layer Coordination?

| Problem | Without Coordination | With Coordination |
|---------|---------------------|-------------------|
| Transaction rollback | Cache invalidated, stale data served | Cache unchanged, correct data served |
| Handler exception | Cache invalidated prematurely | Cache unchanged, no side effects |
| Partial failure | Inconsistent cache state | Atomic: all-or-nothing |

### How It Works

The `CachingBehavior` wraps around handlers in the MediatR pipeline:

```
┌───────────────────────────────────────────────────────────────────────┐
│  HTTP Request: POST /orders                                           │
│                                                                       │
│  1. Controller                                                        │
│     └── _mediator.Send(CreateOrderCommand)                            │
│                                                                       │
│  2. MediatR Pipeline                                                  │
│     ├── LoggingBehavior                                               │
│     ├── ValidationBehavior                                            │
│     └── CachingBehavior ────────────────────────────────────────┐     │
│         │                                                       │     │
│         │  3. Handler (CreateOrderHandler)                      │     │
│         │     ├── BeginTransaction                              │     │
│         │     ├── Validate prescription                         │     │
│         │     ├── Decrement refills                             │     │
│         │     ├── Create order                                  │     │
│         │     └── CommitTransaction                             │     │
│         │         ↓                                             │     │
│         │     Returns order ✓                           　　　  │     │
│         │                                                       │     │
│         │  4. CachingBehavior (ONLY after handler succeeds)     │     │
│         │     └── InvalidateCache(command.CacheKeysToInvalidate)│     │
│         │         • "orders:all"                                │     │
│         │         • "orders:paged:*"                            │     │
│         │         • "orders:patient:{patientId}"                │     │
│         └───────────────────────────────────────────────────────┘     │
│                                                                       │
│  5. Return to Controller → HTTP Response                              │
└───────────────────────────────────────────────────────────────────────┘
```

### CachingBehavior Implementation

```csharp
// src/Application/Behaviors/CachingBehavior.cs
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    // For queries: check cache first
    if (request is ICacheableQuery cacheableQuery)
    {
        return await HandleCacheableQuery(cacheableQuery, next);
    }

    // Execute the handler (includes any DB transaction)
    var response = await next();  // ← Handler runs here

    // ONLY invalidate cache if handler succeeded (no exception thrown)
    if (request is ICacheInvalidatingCommand invalidatingCommand)
    {
        InvalidateCache(invalidatingCommand);  // ← Happens AFTER success
    }

    return response;
}
```

### ICacheableQuery (for Reads)

Implement on queries to enable automatic caching:

```csharp
public record GetOrderByIdQuery(Guid OrderId) : IRequest<PrescriptionOrder?>, ICacheableQuery
{
    public string CacheKey => $"order:{OrderId}";
    public int? CacheTtlSeconds => 300;  // 5 minutes (null = use default)
    public bool BypassCache => false;    // Set true to force DB read
}
```

| Property | Description |
|----------|-------------|
| `CacheKey` | Unique key for this query result |
| `CacheTtlSeconds` | Optional TTL override (null = use config default) |
| `BypassCache` | Skip cache and read from DB (useful for refresh) |

### ICacheInvalidatingCommand (for Writes)

Implement on commands to declare which cache keys to invalidate:

```csharp
public record CreateOrderCommand(
    Guid PatientId,
    Guid PrescriptionId,
    string? Notes
) : IRequest<PrescriptionOrder>, ICacheInvalidatingCommand
{
    public IEnumerable<string> CacheKeysToInvalidate =>
    [
        "orders:all",                          // Exact key
        "orders:paged:*",                      // Prefix pattern (all paged results)
        $"orders:patient:{PatientId}",         // Patient-specific
        $"orders:patient:{PatientId}:paged:*"  // Patient-specific paged results
    ];
}
```

| Pattern | Description |
|---------|-------------|
| `"orders:all"` | Invalidate exact key |
| `"orders:paged:*"` | Invalidate all keys starting with `orders:paged:` |

### Transaction Coordination Example

When a handler uses transactions, cache invalidation respects the transaction outcome:

```csharp
public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, PrescriptionOrder>
{
    public async Task<PrescriptionOrder> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        // Execute within transaction
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var prescription = await _prescriptionRepo.GetByIdAsync(cmd.PrescriptionId, ct)
                ?? throw new NotFoundException("Prescription not found");

            if (prescription.RefillsRemaining <= 0)
                throw new ValidationException("No refills remaining");

            prescription.RefillsRemaining--;
            await _prescriptionRepo.UpdateAsync(prescription, ct);

            var order = new PrescriptionOrder { ... };
            await _orderRepo.AddAsync(order, ct);

            return order;  // Transaction commits here
        }, ct);

        // If we reach here, transaction committed successfully
        // CachingBehavior will now invalidate cache keys

        // If exception thrown above, transaction rolled back
        // CachingBehavior will NOT invalidate cache (exception propagates)
    }
}
```

### Comparison: Application vs Infrastructure Caching

| Aspect | Application Layer (MediatR) | Infrastructure Layer (Attributes) |
|--------|----------------------------|-----------------------------------|
| **Location** | `CachingBehavior` pipeline | `CacheActionFilter` on controllers |
| **Cache declaration** | On Command/Query classes | On Controller action methods |
| **Transaction aware** | ✅ Yes (invalidates after handler success) | ❌ No (invalidates in `finally` block) |
| **When to use** | Commands with DB transactions | Simple GET response caching |
| **Invalidation timing** | After successful handler execution | After action completes (success or fail) |

### When to Use Each Approach

```
┌─────────────────────────────────────────────────────────────────┐
│           Does the operation modify data in a transaction?       │
└──────────────────────────┬──────────────────────────────────────┘
                           │
            Yes ───────────┼─────────── No
             │             │             │
             ▼             │             ▼
   ┌───────────────────┐   │   ┌─────────────────────────────────┐
   │ Application Layer │   │   │ Is it a simple read-only GET?   │
   │ ICacheInvalidating│   │   └───────────────┬─────────────────┘
   │ Command           │   │                   │
   └───────────────────┘   │    Yes ───────────┼─────────── No
                           │     │             │             │
                           │     ▼             │             ▼
                           │ ┌──────────────┐  │   ┌──────────────────┐
                           │ │ Infrastructure│  │   │ Application Layer│
                           │ │ [RemoteCache] │  │   │ ICacheableQuery  │
                           │ └──────────────┘  │   └──────────────────┘
```

**Use Application Layer when:**
- Command modifies data within a transaction
- Need cache invalidation only on success
- Command knows which cache keys are affected

**Use Infrastructure Layer when:**
- Simple GET endpoint caching
- No transaction coordination needed
- Want declarative caching via attributes

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        API Request                              │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  ASP.NET Core Pipeline                                          │
│  ┌─────────────────┐ ┌──────────────────┐ ┌──────────────────┐  │
│  │ Authentication  │→│  Authorization   │→│ CacheMiddleware  │  │
│  └─────────────────┘ └──────────────────┘ └────────┬─────────┘  │
└────────────────────────────────────────────────────┼────────────┘
                                                     │
                    ┌───────────────────────────────┐│
                    │   Cache Services              ││
                    │                               ││
                    │ ┌───────────┐  ┌───────────┐  ││
                    │ │LocalCache │  │RemoteCache│  ││
                    │ │ Service   │  │ Service   │  ││
                    │ │(Memory)   │  │ (Redis)   │  ││
                    │ └───────────┘  └───────────┘  ││
                    └───────────────────────────────┘│
                                                     │
                    Cache Miss                       │
                                                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Controller Action                         │
│                    (Repository / Database)                      │
└─────────────────────────────────────────────────────────────────┘
```

> **Note**: Local cache is for static data only - it is NOT invalidated. Use Remote cache for dynamic data.

## Environment-Specific Configurations

| Environment | Config File | Local | Remote | Local MaxItems |
|-------------|-------------|-------|--------|----------------|
| Base (disabled) | appsettings.json | ❌ | ❌ | 10000 |
| Local Development | appsettings.local.json | ✅ | ✅ | 5000 |
| Development | appsettings.dev.json | ✅ | ✅ | 5000 |
| Staging | appsettings.stage.json | ✅ | ✅ | 20000 |
| Production | appsettings.prod.json | ✅ | ✅ | 50000 |

## Failure Handling

The cache is designed to be **resilient** - it should never break your application.

### Startup Behavior (Fail-Fast)

When Remote cache (Redis) is enabled, the application **requires Redis to be available at startup**:

- `AbortOnConnectFail = true` ensures the app fails to start if Redis is unreachable
- This prevents deploying with misconfigured Redis
- Health checks in orchestrators (Kubernetes, etc.) will detect the failure

```csharp
// Redis must be healthy at startup - fail fast
configurationOptions.AbortOnConnectFail = true;
```

### Runtime Behavior (Graceful Degradation)

If Redis becomes unavailable **after startup**, the application continues to work:

| Operation | Behavior on Redis Failure |
|-----------|---------------------------|
| Cache Get | Returns cache miss, falls through to MongoDB |
| Cache Set | Logs error, continues (data still saved to MongoDB) |
| Cache Delete | Logs error, continues |
| Lock Acquire | Logs error, continues without lock |

This means:
- **No 500 errors** due to cache failures
- Application continues serving requests (just slower)
- All operations are logged at `Error` level for monitoring
- Redis auto-reconnects when available again (`ExponentialRetry` policy)

### Why This Design?

1. **Cache is an optimization, not critical path** - Data is always in MongoDB
2. **High availability** - Cache failures shouldn't cause outages
3. **Self-healing** - Auto-reconnection when Redis recovers
4. **Observable** - All failures are logged for alerting

### Monitoring Recommendations

- Alert on `Error` level logs containing "Failed to" and "cache"
- Monitor cache hit ratio - sudden drops indicate Redis issues
- Use the `/health` endpoint to detect degraded cache state

## Health Checks

When Remote cache (Redis) is enabled, a health check endpoint monitors Redis connectivity:

```
GET /health
```

The Redis health check is automatically registered when `Remote.Enabled = true`.

## Multi-Region Caching and the CAP Theorem

This section explains why **zero staleness with multiple cache instances is fundamentally impossible** and the trade-offs large-scale applications must make.

### The CAP Theorem

The CAP theorem states that a distributed system can only guarantee **two of three** properties:

```
                         ┌─────────────────┐
                         │  CONSISTENCY    │
                         │  All nodes see  │
                         │  same data at   │
                         │  same time      │
                         └────────┬────────┘
                                  │
                    You can only pick 2
                                  │
              ┌───────────────────┼───────────────────┐
              │                   │                   │
              ▼                   │                   ▼
    ┌─────────────────┐           │         ┌─────────────────┐
    │  AVAILABILITY   │           │         │  PARTITION      │
    │  Every request  │           │         │  TOLERANCE      │
    │  gets a response│           │         │  System works   │
    │  (no timeouts)  │           │         │  despite network│
    │                 │           │         │  failures       │
    └─────────────────┘           │         └─────────────────┘
                                  │
                    ┌─────────────┴─────────────┐
                    │   THE REAL CHOICE         │
                    ├───────────────────────────┤
                    │                           │
                    │ In distributed systems,   │
                    │ partitions WILL happen.   │
                    │ P is REQUIRED.            │
                    │                           │
                    │ The choice is: CP or AP   │
                    │                           │
                    ├───────────────────────────┤
                    │ CP: Consistent + Partition│
                    │     (Banks, MongoDB Atlas)│
                    │     → May be unavailable  │
                    │                           │
                    │ AP: Available + Partition │
                    │     (Cassandra, DynamoDB) │
                    │     → May be stale        │
                    └───────────────────────────┘
```

> 💡 **Fun Fact: CA Doesn't Exist in the Real World**
>
> The CAP theorem offers three combinations (CP, AP, CA), but **no production distributed system chooses CA**. Why? Because network partitions are inevitable—cables get cut, datacenters lose power, routers fail. In distributed systems, Partition Tolerance isn't optional; it's reality.
>
> When a "CA" system (like a single PostgreSQL server) faces a network partition, it simply becomes unavailable—making it effectively CP! The theoretical "CA" option only exists for single-node systems that aren't really distributed at all.
>
> **The real-world choice is always: CP or AP.** For example, MongoDB Atlas defaults to CP (consistency) but can be configured for AP (availability) by adjusting write concern and read preferences.

### Why This Application Uses Single Redis

This application uses a **single Redis instance** for caching, which means:

| Aspect | Behavior |
|--------|----------|
| **Within datacenter** | Consistent and available (assuming reliable network) |
| **During Redis failure** | Sentinel promotes replica, brief unavailability |
| **During network partition** | System degrades (falls back to DB) |

This is acceptable because:
- Cache is an optimization, not critical path (MongoDB is source of truth)
- Single-region deployment assumed
- Redis Sentinel handles node failures (not network partitions)

### Multi-Region Architecture Challenges

Large applications often have this architecture:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     MULTI-REGION ARCHITECTURE                               │
│                                                                             │
│   US-East Region                              EU-West Region                │
│   ┌─────────────────┐                        ┌─────────────────┐            │
│   │  Redis-US       │                        │  Redis-EU       │            │
│   │  (local cache)  │                        │  (local cache)  │            │
│   └────────┬────────┘                        └────────┬────────┘            │
│            │                                          │                     │
│   ┌────────┴────────┐                        ┌────────┴────────┐            │
│   │  API Cluster    │                        │  API Cluster    │            │
│   │  (US users)     │                        │  (EU users)     │            │
│   └────────┬────────┘                        └────────┬────────┘            │
│            │                                          │                     │
│            └──────────────────┬───────────────────────┘                     │
│                               ▼                                             │
│                    ┌─────────────────────┐                                  │
│                    │  MongoDB Primary    │                                  │
│                    │  (Single source of  │                                  │
│                    │   truth)            │                                  │
│                    └─────────────────────┘                                  │
│                                                                             │
│   PROBLEM: User updates data in US-East                                     │
│            → Redis-US invalidated ✓                                         │
│            → Redis-EU still has stale data ✗                                │
│            → EU user reads stale data until invalidation propagates         │
│                                                                             │
│   STALENESS WINDOW: Network propagation delay (50-500ms cross-region)       │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Approaches to Minimize Staleness (Not Eliminate)

#### 1. Pub/Sub Cache Invalidation

```
Write in US-East:
  1. Update MongoDB
  2. Invalidate Redis-US
  3. Publish "INVALIDATE key" to message bus
           │
           ▼
    ┌─────────────────────────────────────────┐
    │  Message Bus (Kafka, RabbitMQ, SNS)     │
    └─────────────────────────────────────────┘
           │                    │
           ▼                    ▼
      Redis-US              Redis-EU
      (already done)        (receives message, invalidates)

Staleness Window: Message propagation time (50-200ms)
```

#### 2. Version-Based Validation

```csharp
// Cache includes version number
public async Task<Patient?> GetPatientAsync(Guid id)
{
    var cached = _cache.Get<CachedItem<Patient>>($"patient:{id}");
    if (cached != null)
    {
        // Lightweight version check against MongoDB
        var dbVersion = await _db.GetVersionAsync(id);
        if (cached.Version == dbVersion)
            return cached.Data;  // Still valid

        _cache.Remove($"patient:{id}");  // Stale, remove
    }

    // Fetch fresh from DB
    return await _db.GetPatientAsync(id);
}
```

**Trade-off**: Zero staleness, but every read requires DB version check.

#### 3. MongoDB Change Streams

```csharp
// Each region subscribes to MongoDB changes
var cursor = await _collection.WatchAsync();
await foreach (var change in cursor.ToAsyncEnumerable())
{
    _localRedis.Remove($"patient:{change.DocumentKey["_id"]}");
}
```

**Trade-off**: Near real-time, but still has propagation delay.

#### 4. Hybrid by Data Criticality

| Data Type | Strategy | Staleness |
|-----------|----------|-----------|
| Financial (balances, transactions) | No cache, always DB | Zero |
| Critical (patient records) | Short TTL (5s) + Pub/Sub | < 5 seconds |
| Important (orders, profiles) | Medium TTL (60s) + Pub/Sub | < 60 seconds |
| Reference (drug lists, codes) | Long TTL, manual refresh | Hours/days OK |

### Why Bank Apps Are Slow: The Consistency Tax

Yes, the CAP theorem explains much of why banking applications are slower:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    WHY BANKS CHOOSE CONSISTENCY                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  WHAT BANKS CANNOT TOLERATE:                                                 │
│  ───────────────────────────                                                 │
│  • Double-spending (read stale balance, approve overdraft)                   │
│  • Lost transactions (write not replicated before failover)                  │
│  • Phantom reads (see money that doesn't exist)                              │
│  • Audit gaps (regulators require complete history)                          │
│                                                                              │
│  WHAT BANKS SACRIFICE FOR CONSISTENCY:                                       │
│  ─────────────────────────────────────                                       │
│  • Speed (synchronous replication across datacenters)                        │
│  • Availability (may reject requests during network issues)                  │
│  • Caching (often no cache, or very short-lived)                             │
│  • User experience (loading spinners, timeouts)                              │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### The "Consistency Tax" in Banking

| Operation | E-Commerce (AP) | Banking (CP) |
|-----------|-----------------|--------------|
| **Read balance** | Read from cache (~1ms) | Read from primary DB (~50-100ms) |
| **Transfer money** | Async, eventual | Sync, 2-phase commit (~200-500ms) |
| **Cross-region** | Local cache hit | Route to primary region |
| **Network partition** | Serve stale data | Reject request (503) |
| **Replication** | Async (fast) | Sync (wait for all replicas) |

#### What Makes Bank Apps Slow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  User clicks "Transfer $100"                                                 │
│                                                                              │
│  1. [50ms]   SSL/TLS handshake, authentication                               │
│  2. [100ms]  Fraud detection service check                                   │
│  3. [50ms]   Read source account (no cache, must be fresh)                   │
│  4. [50ms]   Read destination account (no cache)                             │
│  5. [100ms]  Acquire distributed locks on both accounts                      │
│  6. [200ms]  Two-phase commit:                                               │
│              - Prepare: Write to primary, wait for replica ACK               │
│              - Commit: Confirm on all nodes                                  │
│  7. [50ms]   Write audit log (synchronous, compliance)                       │
│  8. [50ms]   Release locks                                                   │
│  9. [50ms]   Send confirmation                                               │
│  ─────────────────────────────────────────────                               │
│  TOTAL: ~700ms                                                               │
│                                                                              │
│  Compare to social media "Like" button:                                      │
│  1. [1ms]    Write to local cache                                            │
│  2. [async]  Eventually persist to DB                                        │
│  TOTAL: ~1ms (user sees instant feedback)                                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Choosing the Right Trade-Off

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     CHOOSE YOUR TRADE-OFF                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  "Users can see slightly outdated data"                                      │
│       → AP (Available + Partition-tolerant)                                  │
│       → Use distributed cache, eventual consistency                          │
│       → Examples: Social media, news, product catalogs                       │
│                                                                              │
│  "Users must ALWAYS see correct data, even if slow"                          │
│       → CP (Consistent + Partition-tolerant)                                 │
│       → No cache or version-validated cache                                  │
│       → Examples: Banking, inventory, medical records                        │
│                                                                              │
│  "We're single-region and need both speed and consistency"                   │
│       → CA (Consistent + Available)                                          │
│       → Single Redis with locks (this application)                           │
│       → Examples: Internal apps, regional services                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Cache Economics & Decision Framework

Before implementing caching, evaluate whether the benefits outweigh the costs. Caching adds complexity, and in some scenarios, it can hurt more than help.

### The Cache Value Equation

```
Cache Value = (Read Frequency × DB Cost Saved) - (Write Frequency × Invalidation Cost + Consistency Cost)
```

### When NOT to Cache

Caching is **not beneficial** when:

| Scenario | Why Cache Hurts |
|----------|-----------------|
| **Write-heavy workloads** | Invalidation overhead exceeds read benefits |
| **Real-time data** | Data changes faster than cache can keep up |
| **Strong consistency required** | Locking overhead negates performance gains |
| **Low read frequency** | Cache misses dominate, wasting memory |
| **High cardinality data** | Too many unique keys, poor hit rates |

### Decision Matrix: Read:Write Ratio × Consistency Requirements

This matrix helps decide whether to cache and which consistency mode to use:

| Read:Write Ratio | Eventual Consistency | Strong Consistency | Serializable |
|------------------|---------------------|-------------------|--------------|
| **100:1** (read-heavy) | ✅ **Cache** - Maximum benefit | ✅ **Cache** - Good benefit | ⚠️ **Cache** - Moderate benefit |
| **10:1** | ✅ **Cache** - Good benefit | ✅ **Cache** - Moderate benefit | ⚠️ **Maybe** - Evaluate carefully |
| **2:1** | ⚠️ **Maybe** - Marginal benefit | ⚠️ **Maybe** - Low benefit | ❌ **Skip** - Overhead too high |
| **1:1** (balanced) | ⚠️ **Maybe** - Evaluate carefully | ❌ **Skip** - Negative value | ❌ **Skip** - Significant overhead |
| **1:10** (write-heavy) | ❌ **Skip** - Negative value | ❌ **Skip** - Very negative | ❌ **Skip** - Maximum overhead |

### Consistency Cost Breakdown

Each consistency level adds overhead to write operations:

| Consistency Level | Write Overhead | When Worth It |
|-------------------|----------------|---------------|
| **Eventual** | Low (just invalidate key) | Read:Write > 5:1, staleness OK |
| **Strong** | Medium (invalidate + bypass on concurrent reads) | Read:Write > 10:1, brief staleness OK |
| **Serializable** | High (acquire lock, wait for readers) | Read:Write > 20:1, zero staleness required |

### Hidden Costs of Caching

```
Write Operation WITHOUT Cache:
  → Write to DB
  TOTAL: 1 operation

Write Operation WITH Cache (Serializable):
  → Acquire distributed lock
  → Write to DB
  → Invalidate local cache (this instance)
  → Invalidate remote cache (Redis)
  → Release distributed lock
  → Other instances detect invalidation
  TOTAL: 5+ operations + network round-trips + lock contention
```

### Alternatives for Write-Heavy or Strong-Consistency Systems

When caching doesn't make sense, consider these alternatives:

| Alternative | Best For | Trade-off |
|-------------|----------|-----------|
| **Read Replicas** | Write-heavy, eventual consistency OK | Database handles caching internally |
| **CQRS** | Complex reads, high write volume | Separate read/write models, more complexity |
| **Materialized Views** | Expensive aggregations | Pre-computed at write time, storage cost |
| **No Cache** | Strong consistency, moderate load | Simpler architecture, higher DB load |
| **CDN/Edge Cache** | Static or semi-static content | Limited to HTTP responses |

### Example: This Application's Entities

| Entity | Typical Ratio | Consistency Need | Cache Recommendation |
|--------|---------------|------------------|---------------------|
| **Drug Reference Data** | 1000:1 | Eventual | ✅ Local cache (static) |
| **Patient Profile** | 50:1 | Strong | ✅ Remote cache (Strong) |
| **Prescription** | 20:1 | Strong | ✅ Remote cache (Strong) |
| **Order** | 5:1 | Serializable | ⚠️ Evaluate - marginal benefit |
| **Order Status Updates** | 1:2 | Strong | ❌ Skip cache |
| **Audit Logs** | 0:1 (write-only) | N/A | ❌ Never cache |
| **Real-time Inventory** | 1:10 | Serializable | ❌ Skip cache |

### Simple Decision Rules

1. **Read:Write < 5:1** → Probably don't cache
2. **Serializable + Read:Write < 20:1** → Definitely don't cache
3. **Write-only data** → Never cache
4. **Static reference data** → Always cache (Local, infinite TTL)
5. **When in doubt** → Start without cache, add if metrics justify

> 💡 **Tip**: Measure your actual read:write ratios in production before implementing caching. Assumptions about access patterns are often wrong.

### Summary

| Scenario | Recommendation |
|----------|----------------|
| Single region, moderate scale | Single Redis with locks (this app) ✓ |
| Multi-region, staleness OK | Distributed cache + Pub/Sub invalidation |
| Multi-region, zero staleness | No cache, or version-validated reads |
| Financial/critical data | No cache, synchronous DB reads |
| Reference/static data | Aggressive caching, long TTL |
| Write-heavy workloads | No cache, use read replicas or CQRS |
| Real-time data with strong consistency | No cache, optimize DB queries |

> ⚠️ **This application supports single Redis only.** For multi-region deployments requiring zero staleness, either route all traffic to a single region or bypass the cache for critical operations.
