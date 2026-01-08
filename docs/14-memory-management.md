# Memory Management for Kubernetes

This document describes the .NET GC (Garbage Collection) configuration for running in Kubernetes pods.

## The Problem

By default, .NET uses **all available memory** in a container. In Kubernetes, this causes issues:

1. **No headroom for system processes** - The OS, kubelet, and sidecars need memory too
2. **OOM kills** - Kubernetes kills pods that exceed their memory limit
3. **No graceful degradation** - Instead of slowing down, the pod dies

## Solution: GC Heap Hard Limit

We configure .NET to limit its heap usage to a percentage of available memory.

### Environment Variables

Set in `Dockerfile.eks`:

```dockerfile
ENV DOTNET_GCHeapHardLimitPercent=4B \
    DOTNET_gcServer=1 \
    DOTNET_gcConcurrent=1 \
    DOTNET_GCConserveMemory=9
```

| Variable | Value | Description |
|----------|-------|-------------|
| `DOTNET_GCHeapHardLimitPercent` | `4B` (75%) | Max heap as hex percentage of container memory |
| `DOTNET_gcServer` | `1` | Use Server GC (better for multi-threaded) |
| `DOTNET_gcConcurrent` | `1` | Enable concurrent GC (lower pause times) |
| `DOTNET_GCConserveMemory` | `9` | Aggressiveness level 0-9 (9 = most aggressive) |

### Hex Percentage Values

`DOTNET_GCHeapHardLimitPercent` uses **hexadecimal** values:

| Percentage | Hex Value | Recommended For |
|------------|-----------|-----------------|
| 50% | `32` | Pods with heavy sidecars (Istio, Datadog) |
| 60% | `3C` | Conservative - lots of headroom |
| 70% | `46` | Balanced - most scenarios |
| 75% | `4B` | **Default** - good balance |
| 80% | `50` | Aggressive - minimal sidecars |

### Memory Budget Example

For a pod with **512Mi** memory limit:

| Component | Memory (75% heap) |
|-----------|-------------------|
| .NET GC Heap | 384 MB (75%) |
| .NET non-GC memory | ~50 MB |
| OS/System | ~30 MB |
| Sidecars (if any) | ~48 MB |
| **Headroom** | ~0 MB |

For a pod with **1Gi** memory limit:

| Component | Memory (75% heap) |
|-----------|-------------------|
| .NET GC Heap | 768 MB (75%) |
| .NET non-GC memory | ~80 MB |
| OS/System | ~50 MB |
| Sidecars (if any) | ~100 MB |
| **Headroom** | ~26 MB |

## Kubernetes Deployment Configuration

Set resource limits in your deployment:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: prescription-api
spec:
  template:
    spec:
      containers:
        - name: api
          resources:
            requests:
              memory: "256Mi"
              cpu: "100m"
            limits:
              memory: "512Mi"
              cpu: "500m"
          env:
            # Override GC settings per environment if needed
            - name: DOTNET_GCHeapHardLimitPercent
              value: "4B"  # 75%
```

## OutOfMemoryException Handling

Even with heap limits, OOM can still occur. The API handles this gracefully:

### Response Format

```http
HTTP/1.1 503 Service Unavailable
Retry-After: 30
Content-Type: application/json

{
  "type": "https://httpstatuses.com/503",
  "title": "ServiceUnavailable",
  "status": 503,
  "message": "The service is temporarily unavailable due to resource constraints. Please retry later.",
  "traceId": "0HN6V..."
}
```

### Client Handling

Clients should implement exponential backoff when receiving 503:

```typescript
async function fetchWithBackoff(url: string, maxRetries = 3): Promise<Response> {
  for (let attempt = 0; attempt < maxRetries; attempt++) {
    const response = await fetch(url);
    
    if (response.status === 503) {
      const retryAfter = parseInt(response.headers.get('Retry-After') || '30', 10);
      const backoff = retryAfter * Math.pow(2, attempt); // Exponential
      await new Promise(resolve => setTimeout(resolve, backoff * 1000));
      continue;
    }
    
    return response;
  }
  throw new Error('Service unavailable after max retries');
}
```

## Monitoring

Monitor these metrics to tune memory settings:

| Metric | Alert Threshold | Action |
|--------|-----------------|--------|
| `dotnet_gc_heap_size_bytes` | >80% of limit | Increase limit or reduce load |
| `container_memory_usage_bytes` | >90% of limit | Increase limit |
| `container_oom_events_total` | Any | Reduce heap % or increase limit |
| HTTP 503 rate | >1% | Scale horizontally |

## Best Practices

1. **Start conservative** - Use 60% (`3C`) and increase if needed
2. **Monitor GC pauses** - If too frequent, increase heap limit
3. **Set memory requests = limits** - Guarantees QoS class "Guaranteed"
4. **Test under load** - Validate settings with realistic traffic
5. **Account for sidecars** - Istio/Envoy uses ~50-100MB

## References

- [.NET Runtime Configuration](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector)
- [Running .NET in Containers](https://devblogs.microsoft.com/dotnet/running-with-server-gc-in-a-small-container-scenario-part-1-hard-limit-for-the-gc-heap/)
- [Kubernetes Resource Management](https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/)

