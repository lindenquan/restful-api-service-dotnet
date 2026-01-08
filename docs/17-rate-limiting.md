# Rate Limiting

This document describes how the API protects itself from overload using **adaptive rate limiting** based on real-time system resource monitoring.

## Overview

The API monitors memory, CPU, and thread pool resources on each request. When the system is under pressure, new requests are immediately rejected with `429 Too Many Requests`, preventing OutOfMemoryException and maintaining service stability.

## Why Adaptive?

| Static Rate Limiting | Adaptive Rate Limiting |
|---------------------|------------------------|
| Fixed limits (e.g., 100 concurrent) | Dynamic based on actual resources |
| Same limit regardless of pod size | Auto-scales with container memory/CPU |
| May OOM before hitting limit | Prevents OOM by rejecting early |
| Requires manual tuning per environment | Works out of the box |

## How It Works

```
                    Incoming Request
                          │
                          ▼
              ┌───────────────────────┐
              │  Check System Metrics │
              │  (every 100ms cached) │
              └───────────────────────┘
                          │
            ┌─────────────┴─────────────┐
            │                           │
       Resources OK              Under Pressure
            │                    (Memory > 85% OR
            │                     ThreadPool > 90%)
            ▼                           │
    ┌───────────────┐                   ▼
    │ Process       │         ┌─────────────────┐
    │ Request       │         │ 429 Response    │
    └───────────────┘         │ + Retry-After   │
                              │ + Reason        │
                              └─────────────────┘
```

## Configuration

```json
{
  "RateLimiting": {
    "Enabled": true,
    "MemoryThresholdPercent": 85,
    "ThreadPoolThresholdPercent": 90,
    "PendingWorkItemsThreshold": 1000,
    "CheckIntervalMs": 100,
    "RetryAfterSeconds": 10
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Enable/disable rate limiting |
| `MemoryThresholdPercent` | `85` | Reject when GC memory load exceeds this % **of heap limit** |
| `ThreadPoolThresholdPercent` | `90` | Reject when thread pool utilization exceeds this % |
| `PendingWorkItemsThreshold` | `1000` | Reject when thread pool queue depth exceeds this |
| `CheckIntervalMs` | `100` | How often to check metrics (ms) |
| `RetryAfterSeconds` | `10` | Retry-After header value |

> **Important**: `MemoryThresholdPercent` is relative to the GC heap limit, not container memory.
> If `DOTNET_GCHeapHardLimitPercent=75` and `MemoryThresholdPercent=85`, rate limiting triggers
> at 85% × 75% = **63.75% of container memory**. This provides headroom before hitting the hard limit.

## Metrics Checked

1. **Memory Pressure** - Uses `GC.GetGCMemoryInfo()` to check:
   - `MemoryLoadBytes / TotalAvailableMemoryBytes` percentage
   - Whether `HighMemoryLoadThreshold` is exceeded

2. **Thread Pool Exhaustion** - Checks worker and IO thread utilization:
   - `(MaxThreads - AvailableThreads) / MaxThreads`

3. **Queue Depth** - `ThreadPool.PendingWorkItemCount`

## 429 Response Format

When rate limiting is active, clients receive a Problem Details JSON response:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 10
Content-Type: application/json

{
  "type": "https://httpstatuses.com/429",
  "title": "Too Many Requests",
  "status": 429,
  "message": "Server is under heavy load. Please retry later.",
  "reason": "Memory: 87.2% >= 85%"
}
```

The `reason` field provides diagnostic information about why the request was rejected.

## Production Settings

For production (in Kubernetes), use more aggressive settings:

```json
{
  "RateLimiting": {
    "Enabled": true,
    "MemoryThresholdPercent": 80,
    "ThreadPoolThresholdPercent": 85,
    "PendingWorkItemsThreshold": 500,
    "CheckIntervalMs": 50,
    "RetryAfterSeconds": 5
  }
}
```

## Kubernetes HPA Configuration

To avoid 429 responses during normal operation, configure Horizontal Pod Autoscaler (HPA) to scale pods **before** rate limiting thresholds are reached. The HPA should trigger at lower resource utilization than the rate limiting thresholds.

### Recommended HPA Settings

For the **default** rate limiting config (`MemoryThresholdPercent: 85`, `ThreadPoolThresholdPercent: 90`):

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: api
  minReplicas: 2
  maxReplicas: 10
  metrics:
    # Scale at 70% CPU - well below thread pool threshold (90%)
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    # Scale at 70% memory - well below memory threshold (85%)
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 70
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 0        # Scale up immediately
      policies:
        - type: Percent
          value: 100                        # Double pods if needed
          periodSeconds: 15
        - type: Pods
          value: 4                          # Or add up to 4 pods
          periodSeconds: 15
      selectPolicy: Max
    scaleDown:
      stabilizationWindowSeconds: 300       # Wait 5 min before scaling down
      policies:
        - type: Percent
          value: 10                         # Scale down slowly (10% at a time)
          periodSeconds: 60
```

### Threshold Relationship

| Metric | HPA Target | Rate Limiting Threshold | Buffer |
|--------|------------|-------------------------|--------|
| CPU | 70% | 90% (ThreadPool) | 20% |
| Memory | 70% | 85% | 15% |

The buffer ensures HPA has time to provision new pods before rate limiting activates.

### For Production Settings

If using production rate limiting config (`MemoryThresholdPercent: 80`, `ThreadPoolThresholdPercent: 85`):

```yaml
metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 60    # 25% buffer from 85%
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 60    # 20% buffer from 80%
```

### Key Principles

1. **HPA thresholds < Rate limiting thresholds** - Always leave a 15-20% buffer
2. **Scale up fast, scale down slow** - Prevents flapping during traffic spikes
3. **Set reasonable maxReplicas** - Based on your budget and expected peak load
4. **Use minReplicas ≥ 2** - Ensures availability during pod restarts
5. **Monitor scaling events** - If you see frequent 429s before max replicas, increase maxReplicas

### When 429s Are Expected

Rate limiting (429 responses) should only occur when:
- HPA has already scaled to `maxReplicas`
- Traffic exceeds your provisioned capacity
- This is intentional protection, not a configuration problem

If you see 429s before reaching maxReplicas, either:
- Lower HPA thresholds (scale earlier)
- Reduce `stabilizationWindowSeconds` for faster scale-up
- Increase the buffer between HPA and rate limiting thresholds

## Client-Side Handling

Clients should implement retry logic when receiving 429 responses:

```typescript
async function fetchWithRetry(url: string, options?: RequestInit): Promise<Response> {
  const response = await fetch(url, options);

  if (response.status === 429) {
    const retryAfter = parseInt(response.headers.get('Retry-After') || '10', 10);
    await new Promise(resolve => setTimeout(resolve, retryAfter * 1000));
    return fetchWithRetry(url, options); // Retry
  }

  return response;
}
```

## Comparison with Resilience Strategy

| Feature | Rate Limiting | Resilience |
|---------|---------------|------------|
| **Direction** | Incoming requests | Outgoing operations |
| **Purpose** | Protect this API from overload | Handle external failures |
| **Response** | 429 to client | Retry internally |
| **Documentation** | This document | `docs/13-resilience-strategy.md` |

## Implementation

**Settings class:** `src/Infrastructure/Api/Middleware/RateLimitingSettings.cs`

**Middleware:** `src/Infrastructure/Api/Middleware/RateLimitingMiddleware.cs`

## Best Practices

1. **Set GC heap limits** - Use `DOTNET_GCHeapHardLimitPercent=75` in containers (see `14-memory-management.md`)
2. **Monitor 429 responses** - Track rate limiting frequency for capacity planning
3. **Use circuit breakers downstream** - Combine with resilience for external calls
4. **Tune thresholds based on load testing** - Lower thresholds = earlier rejection = more safety margin
5. **Check logs for pressure events** - Middleware logs when rate limiting activates/deactivates

