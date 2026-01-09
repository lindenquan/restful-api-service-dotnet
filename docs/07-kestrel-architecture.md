# Kestrel Architecture: Async, Thread Safety & Server Protection

## MediatR Request Pipeline

### How a Request Flows

```
HTTP Request
    │
    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ CONTROLLER (Transient)                                                      │
│   • Receives HTTP request                                                   │
│   • Maps request DTO → Command/Query                                        │
│   • Calls _mediator.Send(command)                                           │
└─────────────────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ MEDIATR PIPELINE (behaviors wrap around handler like middleware)            │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │ 1. LoggingBehavior (Scoped)                                           │  │
│  │    → Log "Handling CreatePatientCommand"                              │  │
│  │    │                                                                  │  │
│  │ ┌──▼────────────────────────────────────────────────────────────────┐ │  │
│  │ │ 2. ValidationBehavior (Scoped)                                    │ │  │
│  │ │    → Run FluentValidation validators                              │ │  │
│  │ │    → If invalid: throw ValidationException (stops here)           │ │  │
│  │ │    │                                                              │ │  │
│  │ │ ┌──▼────────────────────────────────────────────────────────────┐ │ │  │
│  │ │ │ 3. CachingBehavior (Scoped)                                   │ │ │  │
│  │ │ │    → For queries: check cache, return if hit                  │ │ │  │
│  │ │ │    │                                                          │ │ │  │
│  │ │ │ ┌──▼────────────────────────────────────────────────────────┐ │ │ │  │
│  │ │ │ │ 4. HANDLER (Transient)                                    │ │ │ │  │
│  │ │ │ │    → Execute business logic                               │ │ │ │  │
│  │ │ │ │    → Use IUnitOfWork for database operations              │ │ │ │  │
│  │ │ │ │    → Return result                                        │ │ │ │  │
│  │ │ │ └───────────────────────────────────────────────────────────┘ │ │ │  │
│  │ │ │    → For commands: invalidate cache                           │ │ │  │
│  │ │ └───────────────────────────────────────────────────────────────┘ │ │  │
│  │ └───────────────────────────────────────────────────────────────────┘ │  │
│  │    → Log "Handled CreatePatientCommand successfully"                  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ CONTROLLER                                                                  │
│   • Maps result → Response DTO                                              │
│   • Returns HTTP response                                                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Pipeline Behaviors (Registered in Order)

| Behavior | Purpose | Lifetime |
|----------|---------|----------|
| **LoggingBehavior** | Log request/response, timing, errors | Scoped |
| **ValidationBehavior** | Run FluentValidation, throw if invalid | Scoped |
| **CachingBehavior** | Cache queries, invalidate on commands | Scoped |

Registration order matters (outermost → innermost):

```csharp
// src/Application/DependencyInjection.cs
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));     // 1st (outermost)
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));  // 2nd
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));     // 3rd (innermost)
```

### Code Example

```csharp
// Controller
[HttpPost]
public async Task<ActionResult<PatientDto>> Create(CreatePatientRequest request, CancellationToken ct)
{
    var command = new CreatePatientCommand(request.FirstName, request.LastName, ...);
    var patient = await _mediator.Send(command, ct);  // ← Triggers pipeline
    return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
}

// Handler (called after all behaviors pass)
public sealed class CreatePatientHandler : IRequestHandler<CreatePatientCommand, Patient>
{
    private readonly IUnitOfWork _unitOfWork;

    public async Task<Patient> Handle(CreatePatientCommand cmd, CancellationToken ct)
    {
        var patient = new Patient(cmd.FirstName, cmd.LastName, cmd.Email, ...);
        await _unitOfWork.Patients.CreateAsync(patient, ct);
        return patient;
    }
}
```

---

## DI Lifetime by Layer

### Complete Registration Map

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SINGLETON                                       │
│                    (One instance for entire application)                     │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │ • MongoClient (connection pool)                                       │  │
│  │ • Redis ConnectionMultiplexer                                         │  │
│  │ • IMemoryCache                                                        │  │
│  │ • ILogger<T>                                                          │  │
│  │ • Configuration/Settings objects (immutable)                          │  │
│  │ • Resilience Pipelines (Polly)                                        │  │
│  │ • IApiKeyGenerator                                                    │  │
│  │ • ICacheService (LocalCacheService or NullCacheService)               │  │
│  │ • Background Services (SystemMetricsService)                          │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────────┤
│                               SCOPED                                         │
│                      (One instance per HTTP request)                         │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │ • IUnitOfWork (coordinates repositories, tracks changes)              │  │
│  │ • Pipeline Behaviors (LoggingBehavior, ValidationBehavior, etc.)      │  │
│  │ • ICurrentUserService (authenticated user for this request)           │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────────┤
│                              TRANSIENT                                       │
│                    (New instance every time requested)                       │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │ • Controllers                                                         │  │
│  │ • MediatR Handlers (new per Send() call)                              │  │
│  │ • FluentValidation Validators                                         │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Kestrel vs Tomcat: Fundamental Architecture Difference

| Aspect | Tomcat (Java) | Kestrel (.NET) |
|--------|---------------|----------------|
| **I/O Model** | Thread-per-request (blocking) | Async I/O (non-blocking) |
| **Thread Pool** | 200 threads default, 1 per request | ~(CPU cores × 2), shared across all requests |
| **Blocking I/O** | Thread waits (blocks) during DB/HTTP calls | Thread released, continues other work |
| **10,000 concurrent requests** | Needs 10,000 threads (impossible) | Needs ~16 threads (on 8-core machine) |
| **Memory per request** | ~1MB stack per thread | ~few KB per async state machine |
| **Request Timeout** | ✅ 20 seconds default | ❌ **None by default!** |

### Why Kestrel's Async Model Matters

```
TOMCAT (Thread-per-request):
┌─────────────────────────────────────────────────────────────────┐
│ Request 1: [====Thread-1-BLOCKED====][DB Query][====BLOCKED====]│
│ Request 2: [====Thread-2-BLOCKED====][DB Query][====BLOCKED====]│
│ Request 3: [====Thread-3-BLOCKED====][DB Query][====BLOCKED====]│
│ ...                                                             │
│ Request 200: Thread pool exhausted! New requests queue/fail     │
└─────────────────────────────────────────────────────────────────┘

KESTREL (Async I/O):
┌─────────────────────────────────────────────────────────────────┐
│ Thread-1: [Req1]──await──[Req47]──await──[Req203]──await──[Req1]│
│ Thread-2: [Req2]──await──[Req48]──await──[Req204]──await──[Req2]│
│ ...                                                             │
│ 16 threads handle 10,000+ concurrent requests!                  │
└─────────────────────────────────────────────────────────────────┘
```

**Key Insight**: When Kestrel hits `await _db.FindAsync()`, the thread is **released** to handle other requests. When the DB responds, any available thread continues the work.

---

## Thread Safety by Layer

### DI Lifetimes Explained

| Lifetime | New Instance When? | Within Same Request | Thread Safety |
|----------|-------------------|---------------------|---------------|
| **Transient** | Every time you ask for it | Get NEW instance each time | ❌ Not needed |
| **Scoped** | Once per HTTP request | Get SAME instance each time | ❌ Not needed |
| **Singleton** | Once for entire app | Same instance for ALL requests | ⚠️ Needed if mutable |

**Transient vs Scoped - The Key Difference:**

```
HTTP Request 1:
┌─────────────────────────────────────────────────────────────────────────┐
│ Controller needs IUnitOfWork  → gets UnitOfWork-A (SCOPED)              │
│ Controller needs IHandler     → gets Handler-1 (TRANSIENT)              │
│      │                                                                  │
│      ▼                                                                  │
│ Handler needs IUnitOfWork     → gets UnitOfWork-A (SAME instance ✓)     │
│ Handler needs IValidator      → gets Validator-1 (TRANSIENT - new)      │
│      │                                                                  │
│      ▼                                                                  │
│ Behavior needs IUnitOfWork    → gets UnitOfWork-A (SAME instance ✓)     │
│ Behavior needs IHandler       → gets Handler-2 (TRANSIENT - new)        │
└─────────────────────────────────────────────────────────────────────────┘

HTTP Request 2:
┌─────────────────────────────────────────────────────────────────────────┐
│ Controller needs IUnitOfWork  → gets UnitOfWork-B (NEW - different!)    │
└─────────────────────────────────────────────────────────────────────────┘
```

**Why Scoped Matters for UnitOfWork:**

```csharp
// ✅ SCOPED - Same UnitOfWork tracks changes across entire request
Controller: _unitOfWork.Patients.Add(patient);  // UnitOfWork-A
Handler:    _unitOfWork.Orders.Add(order);      // UnitOfWork-A (SAME!)
Finally:    await _unitOfWork.SaveAsync();      // Saves BOTH ✓

// ❌ If UnitOfWork was TRANSIENT - changes would be LOST!
Controller: _unitOfWork.Patients.Add(patient);  // UnitOfWork-A
Handler:    _unitOfWork.Orders.Add(order);      // UnitOfWork-B (DIFFERENT!)
Finally:    await _unitOfWork.SaveAsync();      // Only saves patient! 💥
```

### The Critical Question: Do We Need Locks?

**Short Answer**: Almost never, thanks to .NET's DI scoping and async model.

| Layer | DI Lifetime | Thread Safety | Why |
|-------|-------------|---------------|-----|
| **Controllers** | Transient | ✅ Safe | New instance per injection |
| **MediatR Handlers** | Transient | ✅ Safe | New instance per `Send()` call |
| **Validators** | Transient | ✅ Safe | New instance per validation |
| **Pipeline Behaviors** | Scoped | ✅ Safe | Isolated per request |
| **Repositories** | Scoped | ✅ Safe | Isolated per request |
| **DbContext/UnitOfWork** | Scoped | ✅ Safe | Isolated per request |
| **ILogger** | Singleton | ✅ Safe | Thread-safe by design |
| **IMemoryCache** | Singleton | ✅ Safe | Concurrent dictionary internally |
| **Custom Singleton Services** | Singleton | ⚠️ **NEEDS CARE** | Shared across all requests |
| **Static Fields** | N/A | ⚠️ **NEEDS CARE** | Shared across all requests |

### When You DO Need Thread Safety

Only for **Singleton** services and **static** fields that hold mutable state:

```csharp
// ❌ DANGEROUS - Race condition in singleton
public class BadSingletonService
{
    private int _counter = 0;  // Shared across all requests!

    public void Increment()
    {
        _counter++;  // NOT atomic! Race condition!
    }
}

// ✅ SAFE - Using Interlocked for atomic operations
public class SafeSingletonService
{
    private int _counter = 0;

    public void Increment()
    {
        Interlocked.Increment(ref _counter);  // Atomic!
    }
}

// ✅ SAFE - Using ConcurrentDictionary
public class SafeCacheService
{
    private readonly ConcurrentDictionary<string, object> _cache = new();

    public void Set(string key, object value)
    {
        _cache[key] = value;  // Thread-safe!
    }
}
```

### Our Codebase: Thread Safety Analysis

| Component | Lifetime | Mutable State? | Safe? |
|-----------|----------|----------------|-------|
| `OrdersController` | Transient | No | ✅ |
| `CreateOrderHandler` | Transient | No | ✅ |
| `MongoOrderRepository` | Scoped | No (IMongoCollection is safe) | ✅ |
| `CachingBehavior` | Scoped | No | ✅ |
| `RateLimitingMiddleware` | Singleton | Yes (`_isUnderPressure`, `_lastCheck`) | ⚠️ Uses volatile |
| `SystemMetricsService` | Singleton | No mutable shared state | ✅ |

---

## Kestrel's Missing Request Timeout (Critical Gap!)

### The Problem

```
Server: Tomcat          Server: Kestrel (default)
┌──────────────────┐    ┌──────────────────┐
│ Request comes in │    │ Request comes in │
│ Processing...    │    │ Processing...    │
│ 20 seconds pass  │    │ Infinite loop!   │
│ ⏱️ TIMEOUT!　　　│    │ ...              │
│ Connection closed│    │ ... (forever)    │
│ Thread freed     │    │ Connection held  │
└──────────────────┘    │ Thread stolen    │
                        │ Memory leaked    │
                        └──────────────────┘
```

**Comparison of Default Request Timeouts:**
| Server | Default Timeout |
|--------|-----------------|
| Tomcat | 20 seconds |
| Nginx | 60 seconds |
| IIS | 110 seconds |
| Apache | 300 seconds |
| **Kestrel** | **∞ (None!)** |

### Our Protection: RequestTimeoutMiddleware

We implement `RequestTimeoutMiddleware` to protect against runaway requests:

```csharp
// src/Infrastructure/Api/Middleware/RequestTimeoutMiddleware.cs
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
timeoutCts.CancelAfter(timeout);
context.RequestAborted = timeoutCts.Token;
```

**Configuration** (`appsettings.json`):
```json
"RequestTimeout": {
  "Enabled": true,
  "DefaultTimeoutSeconds": 60,
  "EndpointTimeouts": {
    "/health": 5,
    "/api/v1/orders": 30,
    "/api/v2/orders": 30
  }
}
```

⚠️ **Important:** This timeout covers request processing (controller + DB + serialization), but **NOT** the time for the client to download the response. Response download time is protected by `MinResponseDataRate` which enforces a minimum speed but not a maximum time. See [Graceful Shutdown - Request Timeout vs Response Download Time](18-graceful-shutdown.md#request-timeout-vs-response-download-time) for details.

---

## Complete Server Protection Checklist

### What We Have ✅

| Protection | Implementation | Config |
|------------|----------------|--------|
| **Request Timeout** | `RequestTimeoutMiddleware` | 60s default |
| **Header Timeout** | Kestrel `RequestHeadersTimeout` | 30s (Slowloris prevention) |
| **Body Size Limit** | Kestrel `MaxRequestBodySize` | 4 MB |
| **Header Count Limit** | Kestrel `MaxRequestHeaderCount` | 50 |
| **Slow Client Detection** | Kestrel `MinRequestBodyDataRate` | 240 bytes/sec |
| **Rate Limiting** | `RateLimitingMiddleware` | Memory/CPU based |
| **Connection Keep-Alive** | Kestrel `KeepAliveTimeout` | 2 minutes |
| **Global Exception Handler** | `GlobalExceptionMiddleware` | Catches all |
| **HTTP/2 Stream Limits** | Kestrel `MaxStreamsPerConnection` | 100 |
| **Circuit Breaker** | Polly (external services) | Per-service config |

### Configuration Reference

```json
// config/appsettings.json
{
  "Kestrel": {
    "Limits": {
      // Connection Protection
      "KeepAliveTimeout": "00:02:00",           // Close idle connections
      "RequestHeadersTimeout": "00:00:30",       // Slowloris attack prevention

      // Size Limits
      "MaxRequestBodySize": 4194304,             // 4 MB max body
      "MaxRequestHeaderCount": 50,               // Max headers
      "MaxRequestHeadersTotalSize": 32768,       // 32 KB total headers

      // Slow Client Protection
      "MinRequestBodyDataRate": {
        "BytesPerSecond": 240,                   // Must send at least this fast
        "GracePeriod": "00:00:05"                // Grace period before enforcing
      }
    }
  },

  "RequestTimeout": {
    "Enabled": true,
    "DefaultTimeoutSeconds": 60
  }
}
```

---

## Edge Cases & Gaps to Consider

### 1. ✅ Graceful Shutdown (Implemented)

When Kubernetes sends SIGTERM, in-flight requests complete before shutdown.

**Configuration** (`appsettings.json`):
```json
{
  "GracefulShutdown": {
    "ShutdownTimeoutSeconds": 55  // < K8s terminationGracePeriodSeconds (60s)
  }
}
```

**Kubernetes** (`k8s/deployment.yaml`):
```yaml
spec:
  template:
    spec:
      terminationGracePeriodSeconds: 60
```

See [Graceful Shutdown](18-graceful-shutdown.md) for full documentation.

### 2. ⚠️ Large Response Streaming

For endpoints returning large data (reports, exports):

```csharp
// Problem: Buffering entire response in memory
return Ok(hugeList);  // Loads everything into memory!

// Solution: Stream the response
return new FileStreamResult(stream, "application/json");
// Or use IAsyncEnumerable for streaming JSON
```

### 3. ✅ Background Task Cancellation (Handled)

Our `BackgroundService` implementations properly respect `CancellationToken`:

```csharp
// SystemMetricsService.cs - Properly cancellable
while (await timer.WaitForNextTickAsync(stoppingToken))
{
    LogMetrics("Periodic");
}
```

### 4. ⚠️ Database Connection Pool Exhaustion

MongoDB driver has connection pooling, but under extreme load:

```json
// Consider adding explicit connection pool limits
"MongoDB": {
  "ConnectionString": "mongodb://localhost:27017/?maxPoolSize=100&waitQueueTimeoutMS=5000"
}
```

### 5. ✅ Memory Pressure Protection (Handled)

`RateLimitingMiddleware` monitors memory and rejects requests when under pressure:

```csharp
if (_isUnderPressure)
{
    await RejectRequest(context);  // Returns 503
    return;
}
```

### 6. ⚠️ MongoDB Snapshot Isolation - Write Skew Anomaly

MongoDB uses **snapshot isolation** by default for multi-document transactions. While this provides strong consistency for most use cases, it does **not** prevent **write skew anomalies**.

**Example: Duplicate Patient Creation**

Two API users simultaneously create the same patient:

```
Timeline:
┌────────────────────────────────────────────────────────────────────┐
│ T1 (User A - Create Patient)  │ T2 (User B - Create Patient)      │
├───────────────────────────────┼────────────────────────────────────┤
│ BEGIN                         │ BEGIN                              │
│ Check: patient "John" exists? │                                    │
│ → No (snapshot shows empty)   │                                    │
│                               │ Check: patient "John" exists?      │
│                               │ → No (snapshot also shows empty)   │
│ INSERT patient "John"         │                                    │
│                               │ INSERT patient "John"              │
│ COMMIT ✓                      │                                    │
│                               │ COMMIT ✓  ← Both succeed!          │
└───────────────────────────────┴────────────────────────────────────┘
Result: Duplicate "John" patients in the database!
```

**Why this happens**: Snapshot isolation only detects **write-write conflicts** on the exact same document. Both transactions read "no patient exists" from their snapshots, then each inserts a new document. Since they're inserting different documents (different `_id`s), MongoDB sees no conflict.

**Solutions**:
1. **Unique index** - Create a unique index on patient identifier fields (recommended)
2. **Atomic upsert** - Use `findOneAndUpdate` with `upsert: true`
3. **Application-level locking** - Use distributed locks (Redis, etc.)

**Our Decision**: We are aware of this limitation. For our use case, write skew has a very low probability of occurring (requires exact same millisecond timing from different users). We accept this risk and rely on:
- Unique indexes where critical
- Low collision probability in practice
- Manual deduplication if needed

> ⚠️ **Note**: If your use case requires **serializable isolation** (zero tolerance for write skew), MongoDB's snapshot isolation is insufficient. Consider PostgreSQL or implement application-level serialization.

---

## Summary: Why Kestrel is Safe (With Proper Configuration)

| Concern | Tomcat Approach | Kestrel Approach |
|---------|-----------------|------------------|
| Thread safety | Thread-per-request isolation | Scoped DI + async provides same isolation |
| Request timeout | Built-in 20s default | **Must add middleware!** |
| Resource limits | Thread pool caps concurrency | Rate limiting + Kestrel limits |
| Memory protection | Limited by thread pool size | GC heap limits + rate limiting |

**Key Takeaway**: Kestrel's async model makes thread safety a non-issue for 99% of code. The main gap is the **missing request timeout**, which we've addressed with `RequestTimeoutMiddleware`.

---

## Quick Reference: Thread Safety Rules

```
✅ Scoped/Transient services → No locks needed
✅ ILogger, IMemoryCache → Already thread-safe
✅ Immutable objects → Always thread-safe
⚠️ Singleton with mutable state → Use Interlocked/lock/Concurrent*
⚠️ Static fields → Use Interlocked/lock/Concurrent*
❌ Never share DbContext/UnitOfWork across requests
```
