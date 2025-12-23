# Authentication & Authorization

## Overview

This API uses **API Key authentication** with two user types:

| User Type | Permissions |
|-----------|-------------|
| **Regular** | Create, Read, Update, Soft Delete (sets status to "Deleted") |
| **Admin** | All operations including Hard Delete (permanent removal), Manage Users |

---

## Authentication Flow

```
┌─────────────────────────────────────────────────────────────┐
│                      Client Request                          │
│                 Header: X-API-Key: abc123                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              ApiKeyAuthenticationHandler                     │
│  1. Extract X-API-Key header                                 │
│  2. Hash the key with SHA-256                                │
│  3. Look up hash in database (ApiKeyUser table)             │
│  4. If found → Create ClaimsPrincipal with user info        │
│  5. If not found → Return 401 Unauthorized                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Authorization Policies                      │
│  Check user claims against required policy                   │
│  • RequireAdmin → UserType must be "Admin"                  │
│  • RequireRegularUser → UserType must be "Regular"          │
└─────────────────────────────────────────────────────────────┘
```

---

## API Key Storage

API keys are **never stored in plain text**. Only the SHA-256 hash is stored.

```csharp
// ApiKeyUser entity
public class ApiKeyUser : BaseEntity
{
    public string Email { get; set; }
    public string ApiKeyHash { get; set; }      // SHA-256 hash
    public UserType UserType { get; set; }      // Regular or Admin
    public bool IsActive { get; set; }
}
```

### Hashing Implementation

```csharp
// Infrastructure/Security/ApiKeyHasher.cs
public class ApiKeyHasher : IApiKeyHasher
{
    public string Hash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(bytes);
    }
}
```

---

## Root Admin Initialization

On application startup, a **root admin user** is automatically created if no admin exists.

### Configuration

```json
// appsettings.json
{
  "RootAdmin": {
    "Email": "admin@system.local",
    "InitialApiKey": "your-secure-api-key-here",
    "EnableAutoCreate": true
  }
}
```

### Startup Flow

```
Application Starts
        │
        ▼
RootAdminInitializer (IHostedService)
        │
        ▼
Check if any Admin user exists
        │
        ├── Yes → Do nothing
        │
        └── No → Create root admin with configured API key
                 (hash the key, store in database)
```


---

## Delete Behavior

| User Type | DELETE Operation | Effect |
|-----------|------------------|--------|
| **Regular** | `DELETE /api/v1/orders/{id}` | **Soft Delete** — Sets `IsDeleted=true`, `DeletedAt=now`. Data remains in database. |
| **Admin** | `DELETE /api/v1/orders/{id}` | **Soft Delete** (default) — Same as regular user. |
| **Admin** | `DELETE /api/v1/orders/{id}?permanent=true` | **Hard Delete** — Permanently removes record from database. |

### Soft Delete Fields

All entities inherit from `BaseEntity` which includes:

```csharp
public bool IsDeleted { get; set; }
public DateTime? DeletedAt { get; set; }
public string? DeletedBy { get; set; }
```

### Query Behavior

- All queries automatically filter out soft-deleted records (`IsDeleted=false`)
- Admin users can optionally include deleted records in queries

---

## Admin API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/api-keys` | POST | Create new API key user |
| `/api/v1/admin/api-keys` | GET | List all API key users |
| `/api/v1/admin/api-keys/{id}` | DELETE | Deactivate API key user |

### Create API Key Request

```json
POST /api/v1/admin/api-keys
X-API-Key: {root-admin-key}

{
  "email": "user@example.com",
  "userType": "Regular"  // or "Admin"
}
```

### Response

```json
{
  "id": "guid",
  "email": "user@example.com",
  "apiKey": "generated-api-key-shown-only-once",
  "userType": "Regular"
}
```

> ⚠️ The API key is shown **only once** in the response. It cannot be retrieved later.

---

## Security Best Practices

| Practice | Implementation |
|----------|----------------|
| **Hash API keys** | SHA-256, never store plain text |
| **Change root key immediately** | After first deployment, rotate the initial key |
| **Use HTTPS** | API keys sent in headers must be encrypted |
| **Rate limiting** | Prevent brute force attacks |
| **Audit logging** | Log all admin operations |

