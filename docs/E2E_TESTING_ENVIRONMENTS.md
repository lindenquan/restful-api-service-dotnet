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

```bash
# Automatic (recommended) - starts/stops Docker automatically
just test-api-e2e

# Equivalent to:
just test-api-e2e local

# Manual control
just docker-up
ASPNETCORE_ENVIRONMENT=local dotnet test tests/Tests.Api.E2E
just docker-down
```

#### Dev Environment

```bash
# Uses real dev MongoDB/Redis from appsettings.dev.json
just test-api-e2e dev

# Manual
ASPNETCORE_ENVIRONMENT=dev dotnet test tests/Tests.Api.E2E
```

#### Stage Environment

```bash
# Uses real stage MongoDB/Redis from appsettings.stage.json
just test-api-e2e stage
```

### Web E2E Tests

#### Local Environment (Default)

```bash
# Automatic (recommended) - starts/stops Docker automatically
just web-test-e2e

# Equivalent to:
just web-test-e2e local

# Manual control
just docker-up-api
dotnet test tests/Tests.Web.E2E
just docker-down
```

#### Dev Environment

```bash
# Assumes dev API is running on localhost:5000
# (or update WebE2ETestFixture.ApiBaseUrl)
just web-test-e2e dev
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


### 2. Justfile - Conditional Docker Startup

The justfile only starts Docker when `env=local`:

```bash
# justfile
test-api-e2e env="local":
    #!/usr/bin/env sh
    set -e
    if [ "{{env}}" = "local" ]; then \
        echo "🚀 Starting Docker services..."; \
        docker compose up -d mongodb redis; \
        sleep 10s; \
    fi
    echo "🚀 Running API E2E tests (ASPNETCORE_ENVIRONMENT={{env}})..."
    ASPNETCORE_ENVIRONMENT="{{env}}" dotnet test tests/Tests.Api.E2E --verbosity minimal
    if [ "{{env}}" = "local" ]; then \
        echo "🛑 Stopping Docker services..."; \
        docker compose down; \
    fi
```

**Key Points:**
- Default parameter: `env="local"`
- Conditional Docker: Only when `env=local`
- Environment variable: `ASPNETCORE_ENVIRONMENT={{env}}`

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
- `just test-api-e2e` → local (Docker)
- `just test-api-e2e dev` → dev (real servers)
- `just test-api-e2e stage` → stage (real servers)

### ✅ No Duplication
- Single `docker-compose.yml`
- Single `Dockerfile`
- Single set of tests

## 🚨 Important Notes

### For Local Environment
- **Requires Docker** - MongoDB and Redis must be running
- **Automatic management** - `just test-api-e2e` handles Docker lifecycle
- **Port conflicts** - Ensure ports 27017, 6379, 5000 are available

### For Other Environments
- **No Docker needed** - Uses real servers from config
- **Network access** - Must be able to reach dev/stage/prod servers
- **Credentials** - May need VPN or authentication

### For Web E2E Tests
- **API must be running** - Web tests don't start the API
- **Local**: Use `just web-test-e2e` (starts Docker API)
- **Other envs**: Start API separately, then run tests

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

```bash
# Local (Docker) - default
just test-api-e2e

# Dev (real servers)
just test-api-e2e dev

# Stage (real servers)
just test-api-e2e stage
```

### Run Web E2E tests against different environments

```bash
# Local (Docker) - default
just web-test-e2e

# Dev (assumes API running on localhost:5000)
just web-test-e2e dev
```

### Run all tests in CI/CD

```bash
# Test against dev environment before deploying
just test-api-e2e dev
just web-test-e2e dev

# Or use the ultimate pipeline (uses local by default)
just ultimate
```

## 🎓 Summary

**Key Takeaway**: E2E tests are **environment-agnostic**. They test the API behavior, not the infrastructure.

- **Local environment** → Docker Compose (for development)
- **Other environments** → Real servers (for verification)
- **Same tests** → Different configurations
- **Clear intent** → `just test-api-e2e [env]`

This architecture gives you maximum flexibility while maintaining simplicity and clarity.

