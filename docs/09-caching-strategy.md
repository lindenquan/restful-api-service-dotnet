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

## Health Checks

When L2 (Redis) is enabled, a health check endpoint monitors Redis connectivity:

```
GET /health
```

The Redis health check is automatically registered when `L2.Enabled = true`.

