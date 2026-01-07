# Caching Strategy

This document describes the L1/L2 caching architecture and configuration.

## Overview

The API supports a two-tier caching strategy:
- **L1 (Memory)**: In-memory cache using `IMemoryCache`, local to each instance
- **L2 (Redis)**: Distributed cache shared across all instances

Both layers are optional and independently configurable.

## Cache Configuration

```json
{
  "Cache": {
    "L1": {
      "Enabled": true,
      "Consistency": "Strong",
      "TtlSeconds": 30,
      "MaxItems": 10000
    },
    "L2": {
      "Enabled": true,
      "Consistency": "Strong",
      "ConnectionString": "redis:6379",
      "InstanceName": "PrescriptionApi:",
      "TtlSeconds": 300,
      "ConnectTimeout": 5000,
      "SyncTimeout": 1000,
      "InvalidationChannel": "cache:invalidate"
    }
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `L1.Enabled` | Enable in-memory cache | false |
| `L1.Consistency` | `Strong` or `Eventual` | Strong |
| `L1.TtlSeconds` | L1 cache TTL in seconds | 30 |
| `L1.MaxItems` | Maximum items in L1 cache | 10000 |
| `L2.Enabled` | Enable Redis cache | false |
| `L2.Consistency` | `Strong` or `Eventual` | Strong |
| `L2.ConnectionString` | Redis connection string | localhost:6379 |
| `L2.TtlSeconds` | L2 cache TTL in seconds (0 = infinite) | 0 |
| `L2.InstanceName` | Redis key prefix | PrescriptionApi: |
| `L2.ConnectTimeout` | Redis connection timeout (ms) | 5000 |
| `L2.SyncTimeout` | Redis operation timeout (ms) | 1000 |
| `L2.InvalidationChannel` | Redis pub/sub channel for L1 invalidation | cache:invalidate |

## Consistency Modes

### Understanding Cache Consistency

| Mode | Invalidation Type | Stale Data Window | Use Case |
|------|-------------------|-------------------|----------|
| **Strong** | Pub/Sub invalidation | Milliseconds to seconds (network latency) | Multi-instance production |
| **Eventual** | TTL-based expiration | Up to configured TTL (e.g., 10s, 30s, 60s) | Development, single-instance |
| **No Cache** | N/A | **Zero** (perfect consistency) | When data must never be stale |

> ⚠️ **Important**: Neither Strong nor Eventual provides perfect consistency. If your use case requires **zero tolerance for stale data**, disable all caching.

### Performance vs Consistency Trade-offs

The table below shows estimated performance characteristics for each cache configuration.
All latency values are approximate and depend on network conditions, hardware, and data size.

#### Read Latency (Cache Hit)

| Configuration | Read Latency | Speedup vs No Cache | Notes |
|---------------|--------------|---------------------|-------|
| **No Cache** | 5-50 ms | 1x (baseline) | Every read hits MongoDB |
| **L2 Eventual** | 0.5-2 ms | ~10-50x faster | Redis network round-trip |
| **L2 Strong** | 0.5-2 ms | ~10-50x faster | Same as Eventual for reads |
| **L1 Eventual** | 0.001-0.01 ms | ~1000-5000x faster | In-process memory access |
| **L1 Strong** | 0.001-0.01 ms | ~1000-5000x faster | Same as Eventual for reads |
| **L1 + L2** | 0.001-0.01 ms | ~1000-5000x faster | L1 hit; L2 as fallback |

#### Write Overhead

| Configuration | Write Overhead | Description |
|---------------|----------------|-------------|
| **No Cache** | 0 ms | No cache to update |
| **L2 Eventual** | +0.5-2 ms | Write to Redis |
| **L2 Strong** | +1-3 ms | Write to Redis + pub/sub publish |
| **L1 Eventual** | +0.001 ms | Update local memory |
| **L1 Strong** | +1-3 ms | Update local + pub/sub (requires L2) |
| **L1 + L2 Strong** | +1-3 ms | Write L2 + pub/sub + L1 invalidation |
| **L1 + L2 Eventual** | +0.5-2 ms | Write L2 + update L1 |

#### Complete Trade-off Matrix

| Config | Read (Hit) | Write Overhead | Stale Window | Memory | Best For |
|--------|------------|----------------|--------------|--------|----------|
| No Cache | 5-50ms | 0ms | 0 | None | Perfect consistency required |
| L1 Eventual | ~0.01ms | ~0.001ms | Up to TTL | ~100MB | Single instance, dev |
| L1 Strong (no L2) | ~0.01ms | ~0.001ms | Up to TTL* | ~100MB | ⚠️ Degraded - avoid |
| L2 Eventual | ~1ms | ~1ms | Up to TTL | None | Multi-instance, tolerant |
| L2 Strong | ~1ms | ~2ms | ~ms | None | Multi-instance, critical |
| L1+L2 Eventual | ~0.01ms | ~1ms | Up to L1 TTL | ~100MB | High perf, tolerant |
| L1+L2 Strong | ~0.01ms | ~2ms | ~ms | ~100MB | **Recommended for prod** |

*L1 Strong without L2 falls back to TTL-based (no cross-instance invalidation)

#### Throughput Estimates (Single Instance)

| Configuration | Estimated RPS | CPU Overhead | Notes |
|---------------|---------------|--------------|-------|
| No Cache | 100-500 | Low | MongoDB bottleneck |
| L2 Only | 1,000-5,000 | Low | Redis bottleneck |
| L1 Only | 10,000-50,000 | Medium | Memory + GC pressure |
| L1 + L2 | 10,000-50,000 | Medium | Best latency + resilience |

> **Note**: Actual throughput depends on query complexity, data size, hardware, and concurrent connections. These are order-of-magnitude estimates for typical REST API workloads.

#### Choosing the Right Configuration

```
                    ┌─────────────────────────────────────┐
                    │   Do you need perfect consistency?  │
                    └──────────────────┬──────────────────┘
                                       │
                         Yes ──────────┼────────── No
                          │            │            │
                          ▼            │            ▼
                    ┌─────────┐        │    ┌─────────────────┐
                    │No Cache │        │    │ Multi-instance? │
                    └─────────┘        │    └────────┬────────┘
                                       │             │
                                       │   Yes ──────┼────── No
                                       │    │        │        │
                                       │    ▼        │        ▼
                                       │ ┌───────────┴───┐  ┌──────────────┐
                                       │ │L1+L2 Strong   │  │ L1 Eventual  │
                                       │ │(recommended)  │  │ (simple)     │
                                       │ └───────────────┘  └──────────────┘
```

### Strong Consistency (Pub/Sub Invalidation)

**Mechanism**: Redis pub/sub messaging for cross-instance cache invalidation

- When data is updated, a message is published to Redis
- All instances subscribed to the channel receive the invalidation message
- L1 cache entries are removed immediately upon receiving the message
- **Requires L2 (Redis) to be enabled** - see limitation below

**Stale Data Window**: ~1-100 milliseconds (typical)
- Depends on network latency between instances and Redis
- During the pub/sub message transit, other instances may serve stale data
- In rare cases (network partition, Redis overload), can extend to seconds

**Best for**: Production multi-instance deployments where near-real-time consistency is acceptable

### Eventual Consistency (TTL-Based Expiration)

**Mechanism**: Time-To-Live (TTL) based cache expiration

- Cache entries automatically expire after the configured TTL
- No cross-instance invalidation - each instance manages its own cache
- After data changes, cache continues serving old data until TTL expires

**Stale Data Window**: Up to configured `TtlSeconds`
- If `L1.TtlSeconds = 30`, worst-case stale data = 30 seconds
- Average stale time = TTL / 2 (assuming random access patterns)

| TTL Setting | Max Stale Time | Average Stale Time | Recommended For |
|-------------|----------------|-------------------|-----------------|
| 10 seconds | 10s | ~5s | Development with L2 |
| 30 seconds | 30s | ~15s | Staging/Production single-instance |
| 60 seconds | 60s | ~30s | Read-heavy, change-infrequent data |

**Best for**: Development, single-instance deployments, or when stale data is acceptable

### Disabling Cache for Perfect Consistency

**If your use case requires data to never be stale, disable caching entirely:**

```json
{
  "Cache": {
    "L1": { "Enabled": false },
    "L2": { "Enabled": false }
  }
}
```

This ensures every request reads directly from MongoDB. Trade-off: Higher latency and database load.

### ⚠️ L1 Strong Consistency Limitation

**L1 Strong consistency requires L2 (Redis) to be enabled** for cross-instance invalidation to work.

| L1 Consistency | L2 Enabled | Behavior | Stale Window |
|----------------|------------|----------|--------------|
| Strong | ✅ | Full cross-instance invalidation via Redis pub/sub | ~ms |
| Strong | ❌ | **Degraded**: Each instance has isolated L1 cache | Up to L1 TTL |
| Eventual | ✅ | No invalidation, uses short TTL | Up to L1 TTL |
| Eventual | ❌ | No invalidation, uses short TTL | Up to L1 TTL |

If L1 is set to Strong but L2 is disabled, the application logs a warning at startup:
> L1 cache is configured with Strong consistency but L2 (Redis) is disabled. Strong consistency requires Redis pub/sub for cross-instance invalidation.

**Recommendation**: For multi-instance deployments, always enable L2 when using L1 Strong consistency.

## Cache Scenarios

| L1 | L2 | Implementation |
|----|----|----|
| ❌ | ❌ | `NullCacheService` (no caching) |
| ✅ | ❌ | `MemoryCacheService` only |
| ❌ | ✅ | `RedisCacheService` only |
| ✅ | ✅ | `HybridCacheService` (L1 + L2) |

## Transparent Caching with MediatR

Cache is invisible to handlers via the `CachingBehavior` pipeline behavior.

### Making a Query Cacheable

Implement `ICacheableQuery` on your MediatR query:

```csharp
public record GetPatientQuery(int Id) : IRequest<PatientDto>, ICacheableQuery
{
    public string CacheKey => $"patient:{Id}";
    public int? CacheTtlSeconds => 60; // Optional TTL override
    public bool BypassCache => false;  // Set true to skip cache
}
```

### Invalidating Cache on Commands

Implement `ICacheInvalidatingCommand` on your MediatR command:

```csharp
public record UpdatePatientCommand(int Id, string Name) 
    : IRequest<PatientDto>, ICacheInvalidatingCommand
{
    public IEnumerable<string> CacheKeysToInvalidate => 
        new[] { $"patient:{Id}", "patients:list" };
}
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        API Request                              │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  MediatR Pipeline                                               │
│  ┌─────────────────┐ ┌──────────────────┐ ┌──────────────────┐  │
│  │ LoggingBehavior │→│ValidationBehavior│→│ CachingBehavior  │  │
│  └─────────────────┘ └──────────────────┘ └────────┬─────────┘  │
└────────────────────────────────────────────────────┼────────────┘
                                                     │
                    ┌───────────────────────────────┐│
                    │      ICacheService            ││
                    │  ┌───────────────────────┐    ││
                    │  │   HybridCacheService  │    ││
                    │  └───────────┬───────────┘    ││
                    │              │                ││
                    │   ┌──────────┴──────────┐     ││
                    │   ▼                     ▼     ││
                    │ ┌───────┐           ┌───────┐ ││
                    │ │  L1   │◄─pub/sub─ │  L2   │ |│
                    │ │Memory │ invalidate│Redis  │ ││
                    │ └───────┘           └───────┘ ││
                    └───────────────────────────────┘│
                                                     │
                    Cache Miss                       │
                                                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Request Handler                           │
│                    (Repository / Database)                      │
└─────────────────────────────────────────────────────────────────┘
```

## Environment-Specific Configurations

| Environment | Config File | L1 | L2 | L1 Consistency | L1 TTL |
|-------------|-------------|----|----|----------------|--------|
| Base (disabled) | appsettings.json | ❌ | ❌ | Eventual | 30s |
| Local Development | appsettings.local.json | ✅ | ✅ | Eventual | 10s |
| Development | appsettings.dev.json | ✅ | ✅ | Eventual | 10s |
| Staging | appsettings.stage.json | ✅ | ✅ | Strong | 15s |
| Production | appsettings.prod.json | ✅ | ✅ | Strong | 30s |

## Failure Handling

The cache is designed to be **resilient** - it should never break your application.

### Startup Behavior (Fail-Fast)

When L2 (Redis) is enabled, the application **requires Redis to be available at startup**:

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
| Pub/Sub Invalidation | Logs error, continues |

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

When L2 (Redis) is enabled, a health check endpoint monitors Redis connectivity:

```
GET /health
```

The Redis health check is automatically registered when `L2.Enabled = true`.

