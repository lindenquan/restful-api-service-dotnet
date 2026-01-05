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
| `L1.Consistency` | `Strong` or `Eventual` | Eventual |
| `L1.TtlSeconds` | L1 cache TTL in seconds | 30 |
| `L1.MaxItems` | Maximum items in L1 cache | 10000 |
| `L2.Enabled` | Enable Redis cache | false |
| `L2.Consistency` | `Strong` or `Eventual` | Strong |
| `L2.ConnectionString` | Redis connection string | localhost:6379 |
| `L2.TtlSeconds` | L2 cache TTL in seconds | 300 |
| `L2.InstanceName` | Redis key prefix | PrescriptionApi: |
| `L2.ConnectTimeout` | Redis connection timeout (ms) | 5000 |
| `L2.SyncTimeout` | Redis operation timeout (ms) | 1000 |

## Consistency Modes

### Strong Consistency
- Uses Redis pub/sub for cross-instance cache invalidation
- When data is updated, all instances receive invalidation message
- L1 cache is invalidated immediately across all servers
- Recommended for production with multiple instances

### Eventual Consistency
- No cross-instance invalidation
- Relies on short TTL for L1 cache
- Simpler, lower overhead
- Suitable for development or single-instance deployments

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
│                        API Request                               │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  MediatR Pipeline                                                │
│  ┌─────────────────┐ ┌──────────────────┐ ┌──────────────────┐  │
│  │ LoggingBehavior │→│ValidationBehavior│→│ CachingBehavior  │  │
│  └─────────────────┘ └──────────────────┘ └────────┬─────────┘  │
└────────────────────────────────────────────────────┼────────────┘
                                                     │
                    ┌───────────────────────────────┐│
                    │      ICacheService             ││
                    │  ┌───────────────────────┐    ││
                    │  │   HybridCacheService  │    ││
                    │  └───────────┬───────────┘    ││
                    │              │                ││
                    │   ┌──────────┴──────────┐     ││
                    │   ▼                     ▼     ││
                    │ ┌───────┐         ┌───────┐  ││
                    │ │  L1   │◄─pub/sub─│  L2   │  ││
                    │ │Memory │ invalidate│Redis │  ││
                    │ └───────┘         └───────┘  ││
                    └───────────────────────────────┘│
                                                     │
                    Cache Miss                       │
                                                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Request Handler                            │
│                    (Repository / Database)                       │
└─────────────────────────────────────────────────────────────────┘
```

## Environment-Specific Configurations

| Environment | L1 | L2 | L1 Consistency | L1 TTL |
|-------------|----|----|----------------|--------|
| Development | ✅ | ✅ | Eventual | 10s |
| Staging | ✅ | ✅ | Strong | 15s |
| Production | ✅ | ✅ | Strong | 30s |
| E2E Tests | ❌ | ✅ | - | - |

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

