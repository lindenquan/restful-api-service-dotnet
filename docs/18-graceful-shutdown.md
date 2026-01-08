# Graceful Shutdown

## Overview

Graceful shutdown ensures that when the application receives a termination signal (SIGTERM), it:

1. **Stops accepting new requests** - Load balancer routes traffic elsewhere
2. **Completes in-flight requests** - Active requests finish processing
3. **Releases resources cleanly** - Database connections, file handles closed properly
4. **Exits with success code** - Signals clean shutdown to orchestrator

Without graceful shutdown, requests can be cut mid-processing, causing data corruption or failed transactions.

---

## Kubernetes Shutdown Sequence

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        KUBERNETES SHUTDOWN SEQUENCE                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  T+0s     Pod termination initiated (kubectl delete, rolling update, etc.)  │
│     │                                                                       │
│     ▼                                                                       │
│  T+0s     Pod removed from Service endpoints (parallel with SIGTERM)        │
│     │     ├── Load balancer stops sending NEW traffic                       │
│     │     └── In-flight requests continue                                   │
│     ▼                                                                       │
│  T+0s     SIGTERM sent to container                                         │
│     │     ├── .NET IHostApplicationLifetime.ApplicationStopping fires       │
│     │     ├── Kestrel stops accepting connections                           │
│     │     └── In-flight requests continue processing                        │
│     ▼                                                                       │
│  T+55s    .NET ShutdownTimeout expires (configurable)                       │
│     │     └── Any remaining requests are cancelled                          │
│     ▼                                                                       │
│  T+60s    terminationGracePeriodSeconds expires                             │
│     │     └── SIGKILL sent - process forcefully terminated                  │
│     ▼                                                                       │
│  T+60s    Pod terminated                                                    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Configuration

### Application Settings

```json
// config/appsettings.json
{
  "GracefulShutdown": {
    "ShutdownTimeoutSeconds": 55    // Must be < K8s terminationGracePeriodSeconds
  }
}
```

### Kubernetes Deployment

```yaml
# k8s/deployment.yaml
spec:
  template:
    spec:
      # CRITICAL: Must be >= ShutdownTimeoutSeconds + buffer
      terminationGracePeriodSeconds: 60
```

### Timing Relationship

```
┌────────────────────────────────────────────────────────────┐
│         K8s terminationGracePeriodSeconds: 60s             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │     .NET ShutdownTimeoutSeconds: 55s                 │  │
│  │  ┌───────────────────────────────────────────────┐   │  │
│  │  │    In-flight request processing time          │   │  │
│  │  └───────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                 [5s buffer]│
└────────────────────────────────────────────────────────────┘
```

**Important:** `ShutdownTimeoutSeconds` (55s) must be **less than** `terminationGracePeriodSeconds` (60s) to allow clean exit before SIGKILL.

---

## Implementation

### 1. Host Shutdown Timeout

```csharp
// Program.cs
var gracefulShutdownSettings = builder.Configuration
    .GetSection(GracefulShutdownSettings.SectionName)
    .Get<GracefulShutdownSettings>() ?? new GracefulShutdownSettings();

builder.Host.ConfigureHostOptions(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(
        gracefulShutdownSettings.ShutdownTimeoutSeconds);
});
```

### 2. Lifecycle Event Logging

```csharp
// Program.cs
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Shutdown");

lifetime.ApplicationStopping.Register(() =>
    logger.LogWarning(
        "SIGTERM received. Waiting up to {Timeout}s for in-flight requests...",
        gracefulShutdownSettings.ShutdownTimeoutSeconds));

lifetime.ApplicationStopped.Register(() =>
    logger.LogInformation("Application stopped gracefully."));
```

---

## What Happens During Shutdown

| Component | Behavior on SIGTERM |
|-----------|---------------------|
| **Kestrel** | Stops accepting new connections, waits for in-flight |
| **Controllers** | Continue processing active requests |
| **Background Services** | Receive cancellation token, should stop accepting work |
| **Database Connections** | Returned to pool when requests complete |
| **Health Endpoint** | Still responds (for K8s to track progress) |

---

## Request Timeout vs Response Download Time

⚠️ **Important distinction:**

| Phase | Protected By | Duration |
|-------|-------------|----------|
| Request body upload | `MinRequestBodyDataRate` | Enforced by Kestrel |
| Controller processing | `RequestTimeoutMiddleware` | 60 seconds (configurable) |
| Response body download | `MinResponseDataRate` | **No maximum time!** |

The `RequestTimeout` of 60 seconds covers controller execution, database queries, and serialization. **It does NOT cover the time for the client to download the response.**

### Why This Matters for Graceful Shutdown

A slow client downloading a large response can block shutdown:

```
Response Size: 1 MB
MinResponseDataRate: 240 bytes/sec
Worst-case download time: 1,048,576 / 240 = 72+ minutes!
```

### Mitigations

1. **Kestrel's `MinResponseDataRate`** disconnects clients below 240 bytes/sec (after 5s grace)

2. **Keep responses small** - Paginate large data sets:
   ```csharp
   // ✅ Paginated - small response, fast download
   GET /api/orders?page=1&pageSize=100

   // ❌ Unpaginated - could be huge
   GET /api/orders
   ```

3. **Use streaming for large exports** - Stream with cancellation support:
   ```csharp
   [HttpGet("export")]
   public async IAsyncEnumerable<OrderDto> ExportAll(
       [EnumeratorCancellation] CancellationToken ct)
   {
       await foreach (var order in _repository.StreamAllAsync(ct))
       {
           yield return order;
       }
   }
   ```

   When shutdown occurs, `ct` is cancelled, stopping the stream.

---

## Best Practices

### 1. Keep Request Processing Time Short

```csharp
// ❌ Bad: Long-running endpoint with no timeout
[HttpPost("process-all")]
public async Task<IActionResult> ProcessAll()
{
    await ProcessMillionsOfRecords();  // Could take hours!
    return Ok();
}

// ✅ Good: Bounded processing with cancellation support
[HttpPost("process-batch")]
public async Task<IActionResult> ProcessBatch(CancellationToken ct)
{
    await ProcessBatch(maxRecords: 1000, ct);  // Quick, cancellable
    return Ok();
}
```

### 2. Background Services Must Respect Cancellation

```csharp
public class MyBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        // Cleanup when shutting down
        _logger.LogInformation("Background service stopping gracefully");
    }
}
```

---

## Testing Graceful Shutdown

### Local Testing with Docker

```bash
# Start the container
docker run -d --name test-api prescription-api:latest

# Send requests in background
for i in {1..10}; do curl http://localhost:8080/api/v1/orders & done

# Send SIGTERM (simulates K8s termination)
docker stop --time=60 test-api

# Check logs for graceful shutdown
docker logs test-api | tail -20
```

### Expected Log Output

```
warn: Shutdown[0]
      SIGTERM received. Stopping new request acceptance.
      Waiting up to 55s for in-flight requests to complete...
info: Shutdown[0]
      Application stopped gracefully.
```

### Kubernetes Testing

```bash
# Watch pod logs while terminating
kubectl logs -f deployment/prescription-api &

# Delete pod (triggers graceful shutdown)
kubectl delete pod -l app=prescription-api

# Or trigger rolling update
kubectl set env deployment/prescription-api FORCE_RESTART=$(date +%s)
```

---

## Troubleshooting

### Pod Killed Before Requests Complete

**Symptom:** Logs show SIGKILL, not graceful stop.

**Cause:** `terminationGracePeriodSeconds` too short.

**Fix:** Increase `terminationGracePeriodSeconds` in deployment:
```yaml
spec:
  template:
    spec:
      terminationGracePeriodSeconds: 60
```

### Requests Cancelled During Shutdown

**Symptom:** Requests return 503 or connection reset during deployment.

**Causes:**
1. `ShutdownTimeoutSeconds` too short
2. Long-running requests exceed timeout

**Fix:**
- Increase `ShutdownTimeoutSeconds` in appsettings
- Ensure requests complete within timeout
- Use background jobs for long operations

### Pod Hangs During Shutdown

**Symptom:** Pod takes full 60s to terminate every time.

**Cause:** Something blocking shutdown (e.g., infinite loop, deadlock).

**Debug:**
```bash
# Get thread dump during shutdown
kubectl exec -it pod/prescription-api-xxx -- kill -QUIT 1
```

---

## Related Documentation

| Document | Description |
|----------|-------------|
| [Kestrel Architecture](07-kestrel-architecture.md) | Thread model, request handling |
| [Memory Management](14-memory-management.md) | GC settings for containers |
| [Rate Limiting](17-rate-limiting.md) | Request throttling during load |
| [Observability](16-observability.md) | Logging, metrics, tracing |

