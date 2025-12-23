# Blazor WebAssembly + API Client Architecture

## Overview

This project includes a **Blazor WebAssembly** client that consumes the API using **type-safe, contract-based HTTP clients** powered by **Refit**.

---

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│ 1. DTOs (Pure Data Transfer Objects)                       │
│    - OrderDto, CreateOrderRequest, etc.                    │
│    - NO framework dependencies                             │
│    - Shared between server & client                        │
└─────────────────────────────────────────────────────────────┘
                            ↑
┌─────────────────────────────────────────────────────────────┐
│ 2. Contracts (Pure API Interfaces)                         │
│    - IOrdersApi (pure interface, no ActionResult)          │
│    - Uses DTOs, not framework types                        │
│    - Shared between server & client                        │
└─────────────────────────────────────────────────────────────┘
                            ↑
┌──────────────────────┬──────────────────────────────────────┐
│ 3a. Server           │ 3b. Client                           │
│ (Adapters/Api)       │ (Adapters/ApiClient)                 │
│                      │                                      │
│ OrdersController     │ OrdersApiClient                      │
│ implements logic     │ implements via HTTP (Refit)          │
│ (ASP.NET Core)       │                                      │
└──────────────────────┴──────────────────────────────────────┘
                            ↑
┌─────────────────────────────────────────────────────────────┐
│ 4. Blazor WebAssembly (src/Web)                            │
│    - Consumes ApiClient                                     │
│    - Type-safe API calls                                    │
└─────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
src/
├── Entities/                    # Domain entities (shared)
├── DTOs/                        # Request/Response DTOs (shared)
│   ├── V1/
│   │   ├── OrderDto.cs
│   │   ├── CreateOrderRequest.cs
│   │   └── UpdateOrderRequest.cs
│   └── V2/
│       └── PrescriptionOrderDto.cs
│
├── Contracts/                   # Pure API contracts (shared)
│   ├── V1/
│   │   ├── IOrdersApi.cs       # Pure interface (no ASP.NET Core)
│   │   └── IAdminApi.cs
│   └── V2/
│       └── IOrdersApi.cs
│
├── Adapters/
│   ├── Api/                     # Server-side controllers
│   │   └── Controllers/
│   │       └── V1/
│   │           └── OrdersController.cs  # Implements IOrdersApi
│   │
│   ├── ApiClient/               # Client-side HTTP clients (shared)
│   │   ├── V1/
│   │   │   ├── IOrdersApiClient.cs      # Refit interface
│   │   │   └── IAdminApiClient.cs
│   │   ├── V2/
│   │   │   └── IOrdersApiClient.cs
│   │   ├── Authentication/
│   │   │   └── ApiKeyAuthenticationHandler.cs
│   │   └── ServiceCollectionExtensions.cs
│   │
│   └── Persistence/             # Server-side database
│
└── Web/                         # Blazor WebAssembly app
    ├── Pages/
    │   └── Orders.razor         # Example: Consumes IOrdersApiClient
    └── Program.cs               # Registers API clients
```

---

## Key Components

### 1. **Contracts (Pure API Interfaces)**

**Location:** `src/Contracts/V1/IOrdersApi.cs`

```csharp
using DTOs.V1;

namespace Contracts.V1;

public interface IOrdersApi
{
    Task<IEnumerable<OrderDto>> GetAllAsync(CancellationToken ct = default);
    Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderDto?> UpdateAsync(int id, UpdateOrderRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, bool permanent = false, CancellationToken ct = default);
}
```

**Key Points:**
- ✅ **No `ActionResult<T>`** - just pure domain types
- ✅ **No ASP.NET Core dependencies** - can be used anywhere
- ✅ **Nullable return types** - `OrderDto?` for "not found"

---

### 2. **API Client (Refit-based HTTP Client)**

**Location:** `src/Adapters/ApiClient/V1/IOrdersApiClient.cs`

```csharp
using Contracts.V1;
using DTOs.V1;
using Refit;

namespace Adapters.ApiClient.V1;

public interface IOrdersApiClient : IOrdersApi
{
    [Get("/api/v1/orders")]
    new Task<IEnumerable<OrderDto>> GetAllAsync(CancellationToken ct = default);

    [Get("/api/v1/orders/{id}")]
    new Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct = default);

    [Post("/api/v1/orders")]
    new Task<OrderDto> CreateAsync([Body] CreateOrderRequest request, CancellationToken ct = default);

    [Put("/api/v1/orders/{id}")]
    new Task<OrderDto?> UpdateAsync(int id, [Body] UpdateOrderRequest request, CancellationToken ct = default);

    [Delete("/api/v1/orders/{id}")]
    new Task<bool> DeleteAsync(int id, [Query] bool permanent = false, CancellationToken ct = default);
}
```

**Refit automatically:**
- ✅ Serializes requests to JSON
- ✅ Deserializes responses from JSON
- ✅ Handles HTTP status codes
- ✅ Throws exceptions for errors

---

### 3. **Authentication Handler**

**Location:** `src/Adapters/ApiClient/Authentication/ApiKeyAuthenticationHandler.cs`

```csharp
public sealed class ApiKeyAuthenticationHandler : DelegatingHandler
{
    private readonly string _apiKey;

    public ApiKeyAuthenticationHandler(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add API key header to every request
        request.Headers.Add("X-API-Key", _apiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
```

---

### 4. **Blazor WebAssembly Registration**

**Location:** `src/Web/Program.cs`

```csharp
using Adapters.ApiClient;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register API clients
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001";
var apiKey = builder.Configuration["ApiKey"] ?? "your-api-key-here";

builder.Services.AddPrescriptionOrderApiClients(apiBaseUrl, apiKey);

await builder.Build().RunAsync();
```

---

### 5. **Blazor Component Usage**

**Location:** `src/Web/Pages/Orders.razor`

```razor
@page "/orders"
@using Adapters.ApiClient.V1
@using DTOs.V1
@inject IOrdersApiClient OrdersApi

<h1>Prescription Orders</h1>

@if (orders == null)
{
    <p>Loading...</p>
}
else
{
    <table>
        @foreach (var order in orders)
        {
            <tr>
                <td>@order.Id</td>
                <td>@order.CustomerName</td>
                <td>@order.Status</td>
            </tr>
        }
    </table>
}

@code {
    private IEnumerable<OrderDto>? orders;

    protected override async Task OnInitializedAsync()
    {
        // ✅ Type-safe API call
        orders = await OrdersApi.GetAllAsync();
    }
}
```

---

## Benefits

| Benefit | Description |
|---------|-------------|
| ✅ **Type Safety** | Compile-time checking for API calls |
| ✅ **Code Sharing** | DTOs + Contracts shared between server & client |
| ✅ **No Duplication** | Single source of truth for API shape |
| ✅ **Clean Architecture** | No framework dependencies in DTOs/Contracts |
| ✅ **Easy Mocking** | `IOrdersApi` can be mocked for testing |
| ✅ **Auto-Serialization** | Refit handles JSON automatically |
| ✅ **Auto-Authentication** | API key injected automatically |

---

## Running the Blazor App

### Development

```bash
# Run the API server
dotnet run --project src/Adapters

# Run the Blazor WASM app (in another terminal)
dotnet run --project src/Web
```

### Configuration

Update `src/Web/wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "https://localhost:5001",
  "ApiKey": "your-api-key-here"
}
```

---

## Why This Architecture?

### ❌ **What We DON'T Do**

```csharp
// ❌ BAD: Putting ActionResult in DTOs project
public interface IOrdersApi
{
    Task<ActionResult<OrderDto>> GetByIdAsync(int id);  // ❌ Couples to ASP.NET Core
}
```

### ✅ **What We DO**

```csharp
// ✅ GOOD: Pure interface in Contracts project
public interface IOrdersApi
{
    Task<OrderDto?> GetByIdAsync(int id);  // ✅ Pure, framework-agnostic
}
```

**Why?**
- **Contracts** are pure (no framework dependencies)
- **Server** implements via ASP.NET Core controllers
- **Client** implements via Refit HTTP client
- **Blazor** consumes the client

---

## Summary

- ✅ **3 new projects**: Contracts, ApiClient, Web
- ✅ **Type-safe API calls** via Refit
- ✅ **Shared contracts** between server & client
- ✅ **Clean Architecture** maintained
- ✅ **Auto-authentication** via API key handler
- ✅ **Example Blazor page** included

> *"The Contracts project is the single source of truth for the API shape, shared between server and client."*

