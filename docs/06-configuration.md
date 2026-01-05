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

### Redis

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379",
    "InstanceName": "PrescriptionApi:",
    "ConnectTimeoutMs": 5000,
    "SyncTimeoutMs": 1000,
    "DefaultExpirationMinutes": 30
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
    "PermitLimit": 100,       // Max concurrent requests
    "QueueLimit": 50,         // Max queued requests
    "RejectionMessage": "Too many requests. Please try again later."
  }
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

# Override Redis
Redis__Enabled=true
Redis__ConnectionString=redis:6379

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
| `RedisSettings` | `Redis` |
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
| Redis Enabled | false | true | true |
| Rate Limit | 1000 | 500 | 200 |
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

