# E2E Testing with Environment Support

This document explains how to run E2E tests against different environments.

## 🎯 Overview

E2E tests can run against **any environment**:
- **local** - Uses Docker Compose (localhost MongoDB/Redis)
- **dev** - Uses real dev servers
- **stage** - Uses real stage servers
- **prod** - Uses real production servers (⚠️ use with caution!)

The key insight: **E2E tests are environment-agnostic**. They just need an API to test against.

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

## 📝 Usage

### API E2E Tests

#### Local Environment (Default)

```powershell
# Automatic (recommended) - starts/stops Docker automatically
./build.ps1 test-api-e2e

# Equivalent to:
./build.ps1 test-api-e2e -Env local

# Manual control
./build.ps1 docker-up
$env:ASPNETCORE_ENVIRONMENT="local"; dotnet test tests/Tests.Api.E2E
./build.ps1 docker-down
```

#### Dev Environment

```powershell
# Uses real dev MongoDB/Redis from appsettings.dev.json
./build.ps1 test-api-e2e -Env dev

# Manual
$env:ASPNETCORE_ENVIRONMENT="dev"; dotnet test tests/Tests.Api.E2E
```

#### Stage Environment

```powershell
# Uses real stage MongoDB/Redis from appsettings.stage.json
./build.ps1 test-api-e2e -Env stage
```

## 🔧 How It Works

### 1. API E2E Tests - Environment Detection

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


### 2. build.ps1 - Conditional Docker Startup

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

### 3. Configuration Files

Each environment has its own config file:

```
config/
├── appsettings.json           # Base configuration
├── appsettings.local.json     # localhost:27017, localhost:6379
├── appsettings.dev.json       # dev-mongo.example.com, dev-redis.example.com
├── appsettings.stage.json     # stage-mongo.example.com, stage-redis.example.com
└── appsettings.prod.json      # prod-mongo.example.com, prod-redis.example.com
```

## 🎯 Benefits

### ✅ Environment Agnostic
- Same tests run against any environment
- No hardcoded "e2e" environment
- Tests verify real behavior

### ✅ Flexible Workflow
- **Local development**: Automatic Docker management
- **CI/CD**: Can test against dev/stage before deploying
- **Production verification**: Can run smoke tests against prod

### ✅ Clear Intent
- `./build.ps1 test-api-e2e` → local (Docker)
- `./build.ps1 test-api-e2e -Env dev` → dev (real servers)
- `./build.ps1 test-api-e2e -Env stage` → stage (real servers)

### ✅ No Duplication
- Single `docker-compose.yml`
- Single `Dockerfile`
- Single set of tests

## 🚨 Important Notes

### For Local Environment
- **Requires Docker** - MongoDB and Redis must be running
- **Automatic management** - `./build.ps1 test-api-e2e` handles Docker lifecycle
- **Port conflicts** - Ensure ports 27017, 6379, 5000 are available

### For Other Environments
- **No Docker needed** - Uses real servers from config
- **Network access** - Must be able to reach dev/stage/prod servers
- **Credentials** - May need VPN or authentication

## 📊 Comparison: Before vs After

| Aspect | Before | After |
|--------|--------|-------|
| **Environment** | Hardcoded "e2e" | Dynamic (local/dev/stage/prod) |
| **Config file** | appsettings.e2e.json | appsettings.{env}.json |
| **Docker** | Always required | Only for local |
| **Flexibility** | E2E only | Any environment |
| **Intent** | Unclear | Clear (local vs real) |

## 🔍 Examples

### Run API E2E tests against different environments

```powershell
# Local (Docker) - default
./build.ps1 test-api-e2e

# Dev (real servers)
./build.ps1 test-api-e2e -Env dev

# Stage (real servers)
./build.ps1 test-api-e2e -Env stage
```

### Run all tests in CI/CD

```powershell
# Test against dev environment before deploying
./build.ps1 test-api-e2e -Env dev

# Or use the ultimate pipeline (uses local by default)
./build.ps1 ultimate
```

## 🎓 Summary

**Key Takeaway**: E2E tests are **environment-agnostic**. They test the API behavior, not the infrastructure.

- **Local environment** → Docker Compose (for development)
- **Other environments** → Real servers (for verification)
- **Same tests** → Different configurations
- **Clear intent** → `./build.ps1 test-api-e2e [-Env env]`

This architecture gives you maximum flexibility while maintaining simplicity and clarity.

