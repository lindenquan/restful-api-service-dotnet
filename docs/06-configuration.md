# Configuration

## Environment Names

| Environment | File | Purpose |
|-------------|------|---------|
| `local` | `appsettings.local.json` | Local development (Docker Compose) |
| `dev` | `appsettings.dev.json` | Development environment |
| `stage` | `appsettings.stage.json` | Staging environment |
| `prod` | `appsettings.prod.json` | Production |
| `amr-prod` | `appsettings.amr-prod.json` | AMR region production |
| `amr-stage` | `appsettings.amr-stage.json` | AMR region staging |
| `eu-prod` | `appsettings.eu-prod.json` | EU region production |
| `eu-stage` | `appsettings.eu-stage.json` | EU region staging |

### Environment Chaining

`amr-prod` **extends** `prod`:

```
appsettings.json → appsettings.prod.json → appsettings.amr-prod.json
```

This is configured in `Program.cs`:

```csharp
if (builder.Environment.EnvironmentName == "amr-prod")
{
    builder.Configuration
        .AddJsonFile("appsettings.prod.json", optional: true)
        .AddJsonFile("appsettings.amr-prod.json", optional: true)
        .AddEnvironmentVariables();
}
```

---

## Configuration Sections

### Database

```json
{
  "Database": {
    "Provider": "MongoDB",        // "InMemory" or "MongoDB"
    "InMemoryDatabaseName": "DevDb"
  }
}
```

### MongoDB

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "Username": "admin",
    "Password": "password123",
    "DatabaseName": "prescription_orders",
    "UsersCollection": "users",
    "PrescriptionsCollection": "prescriptions",
    "OrdersCollection": "orders"
  }
}
```

**Authentication:**
- `Username` and `Password` are optional
- If provided, they will be automatically injected into the `ConnectionString`
- Supports both `mongodb://` and `mongodb+srv://` protocols
- Credentials are URL-encoded automatically

### Kestrel Server Limits

The `Kestrel.Limits` section configures the web server's connection handling, timeouts, and request size limits.

```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": null,
      "MaxConcurrentUpgradedConnections": null,
      "KeepAliveTimeout": "00:02:00",
      "RequestHeadersTimeout": "00:00:30",
      "MaxRequestBodySize": 4194304,
      "MaxRequestHeaderCount": 50,
      "MaxRequestHeadersTotalSize": 32768,
      "MaxRequestLineSize": 8192,
      "MaxResponseBufferSize": 65536,
      "MinRequestBodyDataRate": { "BytesPerSecond": 240, "GracePeriod": "00:00:05" },
      "MinResponseDataRate": { "BytesPerSecond": 240, "GracePeriod": "00:00:05" },
      "Http2": {
        "MaxStreamsPerConnection": 100,
        "HeaderTableSize": 4096,
        "MaxFrameSize": 16384,
        "MaxRequestHeaderFieldSize": 8192,
        "InitialConnectionWindowSize": 131072,
        "InitialStreamWindowSize": 98304
      }
    }
  }
}
```

#### Connection Limits

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxConcurrentConnections` | `null` | Unlimited. Adaptive rate limiting protects based on actual resource usage. |
| `MaxConcurrentUpgradedConnections` | `null` | Unlimited WebSocket/HTTP upgrade connections. |

> **Why unlimited?** With adaptive rate limiting enabled, static connection limits are unnecessary. The rate limiting middleware monitors actual memory and CPU usage, rejecting requests only when resources are stressed. This allows the API to handle as many connections as the pod can support.

#### Timeouts

Format: `"HH:MM:SS"` (TimeSpan)

| Setting | Default | Description |
|---------|---------|-------------|
| `KeepAliveTimeout` | `00:02:00` | **2 minutes** - How long to keep idle connections open before closing. |
| `RequestHeadersTimeout` | `00:00:30` | **30 seconds** - Maximum time to receive all request headers. Prevents slow header attacks. |

#### Request Size Limits

| Setting | Default | Human-Readable | Description |
|---------|---------|----------------|-------------|
| `MaxRequestBodySize` | `4194304` | **4 MB** | Maximum request body size. Set to `null` for unlimited (not recommended). |
| `MaxRequestHeaderCount` | `50` | 50 headers | Maximum number of headers per request. |
| `MaxRequestHeadersTotalSize` | `32768` | **32 KB** | Total combined size of all request headers. |
| `MaxRequestLineSize` | `8192` | **8 KB** | Maximum size of the HTTP request line (e.g., `GET /very/long/path HTTP/1.1`). |

#### Response Buffering

| Setting | Default | Human-Readable | Description |
|---------|---------|----------------|-------------|
| `MaxResponseBufferSize` | `65536` | **64 KB** | Response buffer size before flushing to the client. |

#### Slow Client Protection (Slowloris Prevention)

These settings protect against slow-rate DoS attacks by requiring minimum data transfer rates:

| Setting | Default | Description |
|---------|---------|-------------|
| `MinRequestBodyDataRate.BytesPerSecond` | `240` | **~2 Kbps** minimum upload speed required. |
| `MinRequestBodyDataRate.GracePeriod` | `00:00:05` | **5 seconds** grace period before enforcing the rate. |
| `MinResponseDataRate.BytesPerSecond` | `240` | **~2 Kbps** minimum download speed required. |
| `MinResponseDataRate.GracePeriod` | `00:00:05` | **5 seconds** grace period before enforcing the rate. |

#### HTTP/2 Settings

| Setting | Default | Human-Readable | Description |
|---------|---------|----------------|-------------|
| `MaxStreamsPerConnection` | `100` | 100 streams | Maximum concurrent HTTP/2 streams per connection. |
| `HeaderTableSize` | `4096` | **4 KB** | HPACK header compression table size. |
| `MaxFrameSize` | `16384` | **16 KB** | Maximum HTTP/2 frame payload size. |
| `MaxRequestHeaderFieldSize` | `8192` | **8 KB** | Maximum size of a single header field. |
| `InitialConnectionWindowSize` | `131072` | **128 KB** | Connection-level flow control window size. |
| `InitialStreamWindowSize` | `98304` | **96 KB** | Per-stream flow control window size. |

### Cache (Local/Remote)

```json
{
  "Cache": {
    "Local": {
      "Enabled": false,
      "Consistency": "Strong",
      "TtlSeconds": 30,
      "MaxItems": 10000
    },
    "Remote": {
      "Enabled": false,
      "Consistency": "Strong",
      "ConnectionString": "localhost:6379",
      "InstanceName": "PrescriptionApi:",
      "TtlSeconds": 0,
      "ConnectTimeout": 5000,
      "SyncTimeout": 1000,
      "InvalidationChannel": "cache:invalidate"
    }
  }
}
```

### Root Admin

```json
{
  "RootAdmin": {
    "Email": "admin@system.local",
    "InitialApiKey": "change-this-immediately",
    "EnableAutoCreate": true
  }
}
```

### CORS

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.example.com",
      "https://admin.example.com"
    ],
    "AllowedMethods": ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
    "AllowedHeaders": ["Content-Type", "Authorization", "X-API-Key"],
    "AllowCredentials": true
  }
}
```

### Rate Limiting

```json
{
  "RateLimiting": {
    "Enabled": true,
    "MemoryThresholdPercent": 85,      // Reject when memory exceeds this % of heap limit
    "ThreadPoolThresholdPercent": 90,  // Reject when thread pool exceeds this %
    "PendingWorkItemsThreshold": 1000, // Reject when queue depth exceeds this
    "CheckIntervalMs": 100,            // How often to check metrics
    "RetryAfterSeconds": 10            // Retry-After header value
  }
}
```

See `docs/17-rate-limiting.md` for details on how adaptive rate limiting works.

### Request Timeout

```json
{
  "RequestTimeout": {
    "Enabled": true,
    "DefaultTimeoutSeconds": 60,
    "EndpointTimeouts": {
      "/api/v1/reports": 300
    }
  }
}
```

> ⚠️ **Critical:** Kestrel has NO default request processing timeout. Unlike Tomcat (20s), Nginx (60s), or IIS (110s), a request can run forever. A single infinite loop or deadlock will hold a connection indefinitely, eventually exhausting server resources.

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Enable/disable request timeout |
| `DefaultTimeoutSeconds` | `60` | Global timeout for all requests |
| `EndpointTimeouts` | `{}` | Path-specific overrides (e.g., long-running reports) |

**Response on timeout:**
```json
{
  "type": "https://httpstatuses.com/408",
  "title": "Request Timeout",
  "status": 408,
  "message": "Request processing exceeded the timeout of 60 seconds."
}
```

### Swagger

```json
{
  "Swagger": {
    "Enabled": false
  }
}
```

`Swagger.Enabled` controls whether OpenAPI and Swagger UI are exposed:

- In `appsettings.json` it is `false` (disabled by default).
- In `appsettings.dev.json` it is overridden to `true` so Swagger UI is only enabled in the `dev` environment.

---

## Environment Variables

Configuration can be overridden with environment variables using `__` separator:

```bash
# Override MongoDB connection
MongoDB__ConnectionString=mongodb://user:pass@host:27017

# Override Cache (Remote/Redis)
Cache__Remote__Enabled=true
Cache__Remote__ConnectionString=redis:6379

# Override Root Admin
RootAdmin__InitialApiKey=secure-key-here
```

---

## Strongly-Typed Settings

Configuration sections are bound to C# classes in `Infrastructure/Configuration/`:

| Class | Section |
|-------|---------|
| `DatabaseSettings` | `Database` |
| `MongoDbSettings` | `MongoDB` |
| `LocalCacheSettings` | `Cache:Local` |
| `RemoteCacheSettings` | `Cache:Remote` |
| `RootAdminSettings` | `RootAdmin` |
| `CorsSettings` | `Cors` |
| `RateLimitSettings` | `RateLimiting` |

### Usage in Code

```csharp
// Program.cs - Bind settings
var mongoSettings = builder.Configuration
    .GetSection("MongoDB")
    .Get<MongoDbSettings>();

// Or inject via IOptions<T>
public class MyService
{
    private readonly MongoDbSettings _settings;
    
    public MyService(IOptions<MongoDbSettings> options)
    {
        _settings = options.Value;
    }
}
```

---

## Environment-Specific Defaults

| Setting | dev | stage | prod |
|---------|-----|-------|------|
| Database Provider | InMemory | MongoDB | MongoDB |
| Remote Cache Enabled | false | true | true |
| Rate Limiting Enabled | true | true | true |
| Root Admin Auto-Create | true | true | false |
| Log Level | Debug | Information | Warning |

---

## Docker Environment

In `docker-compose.yml`, environment variables override config:

```yaml
services:
  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=prod
      - MongoDB__ConnectionString=mongodb://mongodb:27017
      - Redis__ConnectionString=redis:6379
      - RootAdmin__InitialApiKey=${ROOT_ADMIN_KEY}
```

---

## Security Notes

| Setting | Recommendation |
|---------|----------------|
| `RootAdmin.InitialApiKey` | Change immediately after first deployment |
| `MongoDB.ConnectionString` | Use environment variable in production |
| `Redis.ConnectionString` | Use environment variable in production |
| `Cors.AllowedOrigins` | Never use `*` in production |

