# E2E Testing Guide

This document covers E2E testing for both API and Web, including environment support and quick reference commands.

## 🚀 Quick Commands

### API E2E Tests

```powershell
# Local (Docker) - Default
./build.ps1 test-api-e2e

# Dev environment
./build.ps1 test-api-e2e -Env dev

# Stage environment
./build.ps1 test-api-e2e -Env stage
```

## 🎯 Overview

E2E tests can run against **any environment**:
- **local** - Uses Docker Compose (localhost MongoDB/Redis)
- **dev** - Uses real dev servers
- **stage** - Uses real stage servers
- **prod** - Uses real production servers (⚠️ use with caution!)

The key insight: **E2E tests are environment-agnostic**. They just need an API to test against.

### What Happens

#### Local Environment (`./build.ps1 test-api-e2e`)

```
1. 🚀 Start Docker (MongoDB + Redis)
2. ⏳ Wait 10 seconds
3. 🧪 Run tests with ASPNETCORE_ENVIRONMENT=local
4. 🛑 Stop Docker
```

#### Other Environments (`./build.ps1 test-api-e2e -Env dev`)

```
1. 🧪 Run tests with ASPNETCORE_ENVIRONMENT=dev
   (Uses real dev servers from appsettings.dev.json)
```

## 🏗️ Architecture

### API E2E Tests

```
┌─────────────────────────────────────────────────────────────┐
│ API E2E Tests (Tests.Api.E2E)                               │
│                                                              │
│ Uses WebApplicationFactory to spin up API in-process        │
│ Environment: Set via ASPNETCORE_ENVIRONMENT                 │
│                                                              │
│ ┌──────────────┐                                            │
│ │ Test Fixture │ → Reads appsettings.{env}.json             │
│ └──────────────┘                                            │
│        ↓                                                     │
│ ┌──────────────┐                                            │
│ │  API Server  │ → Connects to MongoDB/Redis from config   │
│ └──────────────┘                                            │
│        ↓                                                     │
│ ┌──────────────┐                                            │
│ │ HTTP Client  │ → Makes requests to API                    │
│ └──────────────┘                                            │
└─────────────────────────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────────────────────────┐
│ MongoDB + Redis                                              │
│                                                              │
│ local:  localhost:27017 / localhost:6379 (Docker Compose)   │
│ dev:    dev-mongo.example.com / dev-redis.example.com        │
│ stage:  stage-mongo.example.com / stage-redis.example.com    │
└─────────────────────────────────────────────────────────────┘
```

### Web E2E Tests

```
┌─────────────────────────────────────────────────────────────┐
│ Web E2E Tests (Tests.Web.E2E)                               │
│                                                              │
│ Uses Playwright to test Blazor WebAssembly app              │
│                                                              │
│ ┌──────────────┐                                            │
│ │   Playwright │ → Opens browser                            │
│ └──────────────┘                                            │
│        ↓                                                     │
│ ┌──────────────┐                                            │
│ │  Web Server  │ → Serves static files (localhost:3000)     │
│ └──────────────┘                                            │
│        ↓                                                     │
│ ┌──────────────┐                                            │
│ │   Browser    │ → Makes API calls to localhost:5000        │
│ └──────────────┘                                            │
└─────────────────────────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────────────────────────┐
│ API Server (must be running separately)                     │
│                                                              │
│ local:  Docker Compose (docker compose up -d)               │
│ dev:    Real dev API server                                 │
│ stage:  Real stage API server                               │
└─────────────────────────────────────────────────────────────┘
```

## 📋 Prerequisites

### For Local Environment
- ✅ Docker installed and running
- ✅ Ports available: 27017 (MongoDB), 6379 (Redis), 5000 (API)

### For Other Environments
- ✅ Network access to dev/stage/prod servers
- ✅ Valid connection strings in config files
- ✅ VPN/authentication if required

## 🔧 Configuration

### Configuration Files

```
config/
├── appsettings.json           # Base configuration
├── appsettings.local.json     # localhost:27017, localhost:6379
├── appsettings.dev.json       # dev-mongo.example.com, dev-redis.example.com
├── appsettings.stage.json     # stage-mongo.example.com, stage-redis.example.com
└── appsettings.prod.json      # prod-mongo.example.com, prod-redis.example.com
```

### Environment Detection

The test fixture reads `ASPNETCORE_ENVIRONMENT` and loads the corresponding config:

```csharp
// tests/Tests.Api.E2E/Fixtures/ApiE2ETestFixture.cs
protected override IHost CreateHost(IHostBuilder builder)
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "local";
    builder.UseEnvironment(environment);

    config
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{environment}.json", optional: true);
}
```

### build.ps1 - Conditional Docker Startup

The build.ps1 script only starts Docker when `Env=local`:

```powershell
# build.ps1 test-api-e2e command
'test-api-e2e' {
    if ($Env -eq 'local') {
        Write-Host "🚀 Starting Docker services (MongoDB + Redis)..."
        docker compose up -d mongodb redis
        Start-Sleep -Seconds 10
    }
    try {
        Write-Host "🚀 Running API E2E tests (ASPNETCORE_ENVIRONMENT=$Env)..."
        $env:ASPNETCORE_ENVIRONMENT = $Env
        dotnet test tests/Tests.Api.E2E --verbosity minimal
    }
    finally {
        if ($Env -eq 'local') {
            Write-Host "🛑 Stopping Docker services..."
            docker compose down
        }
    }
}
```

**Key Points:**
- Default parameter: `$Env = 'local'`
- Conditional Docker: Only when `$Env -eq 'local'`
- Environment variable: `$env:ASPNETCORE_ENVIRONMENT = $Env`

## 🎯 Benefits

| Benefit | Description |
|---------|-------------|
| **Environment Agnostic** | Same tests run against any environment, no hardcoded "e2e" environment |
| **Flexible Workflow** | Local: automatic Docker management. CI/CD: test against dev/stage before deploying |
| **Clear Intent** | `./build.ps1 test-api-e2e` → local, `-Env dev` → dev servers |
| **No Duplication** | Single `docker-compose.yml`, single `Dockerfile`, single set of tests |

## 📊 Comparison: Before vs After

| Aspect | Before | After |
|--------|--------|-------|
| **Environment** | Hardcoded "e2e" | Dynamic (local/dev/stage/prod) |
| **Config file** | appsettings.e2e.json | appsettings.{env}.json |
| **Docker** | Always required | Only for local |
| **Flexibility** | E2E only | Any environment |
| **Intent** | Unclear | Clear (local vs real) |

## 💡 Tips

### Run specific test class

```powershell
# Local
./build.ps1 docker-up
$env:ASPNETCORE_ENVIRONMENT="local"; dotnet test tests/Tests.Api.E2E --filter "OrdersApiE2ETests"
./build.ps1 docker-down

# Dev
$env:ASPNETCORE_ENVIRONMENT="dev"; dotnet test tests/Tests.Api.E2E --filter "OrdersApiE2ETests"
```

### Keep Docker running for multiple test runs

```powershell
# Start Docker once
./build.ps1 docker-up

# Run tests multiple times
$env:ASPNETCORE_ENVIRONMENT="local"; dotnet test tests/Tests.Api.E2E
$env:ASPNETCORE_ENVIRONMENT="local"; dotnet test tests/Tests.Api.E2E --filter "OrdersApiE2ETests"

# Stop Docker when done
./build.ps1 docker-down
```

### Debug tests

```powershell
# Start Docker
./build.ps1 docker-up

# Run with detailed output
$env:ASPNETCORE_ENVIRONMENT="local"; dotnet test tests/Tests.Api.E2E --verbosity detailed

# Or run in IDE with debugger
# (Set ASPNETCORE_ENVIRONMENT=local in launch settings)

# Stop Docker
./build.ps1 docker-down
```

## 🚨 Troubleshooting

### "API is not accessible"
- Check Docker is running: `docker ps`
- Check ports: `netstat -an | findstr "27017 6379 5000"`
- Restart Docker: `./build.ps1 docker-down; ./build.ps1 docker-up-api`

### "Connection refused" (MongoDB/Redis)
- Verify Docker containers: `docker compose ps`
- Check logs: `docker compose logs mongodb redis`
- Restart: `./build.ps1 docker-down; ./build.ps1 docker-up`

### Tests fail on dev/stage
- Verify network access to servers
- Check VPN connection
- Verify credentials in config files
- Test connection: `Invoke-WebRequest http://dev-api.example.com/health`

## 🎓 Summary

**Key Takeaway**: E2E tests are **environment-agnostic**. They test the API behavior, not the infrastructure.

- **Local environment** → Docker Compose (for development)
- **Other environments** → Real servers (for verification)
- **Same tests** → Different configurations
- **Clear intent** → `./build.ps1 test-api-e2e [-Env env]`

This architecture gives you maximum flexibility while maintaining simplicity and clarity.

