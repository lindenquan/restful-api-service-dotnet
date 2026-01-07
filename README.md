# Prescription Order API

A production-ready REST API built with **.NET 10** and **Clean Architecture**.

## Features

- ✅ Clean Architecture with MediatR
- ✅ API versioning (V1, V2)
- ✅ API Key authentication with Admin/Regular user types
- ✅ MongoDB database
- ✅ L1/L2 caching (Memory + Redis) with configurable consistency
- ✅ Rate limiting (concurrency-based)
- ✅ Docker Compose deployment
- ✅ Comprehensive test suite
- ✅ Security Headers
- ✅ Health Checks (MongoDB, Redis, external services)


---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for MongoDB/Redis)
- [PowerShell 7.5+](https://github.com/PowerShell/PowerShell) - Cross-platform automation

#### Optional: VSCode Extensions

- [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) - REST client for testing API endpoints manually using `.rest` files in the `http/` folder

#### Installing PowerShell 7.5+

PowerShell 7.5 requires .NET 9. If you already have .NET 10 installed, install .NET 9 alongside it:

```bash
# Install .NET 9 alongside .NET 10
dnvm install 9.0.308

# Then install PowerShell as a global tool
dotnet tool install --global PowerShell
```

### Run Locally (Development)

```bash
# Clone the repository
git clone <repository-url>
cd restful-api-service-dotnet

# Copy environment file and configure
cp .env.example .env

# Start Docker services (MongoDB + Redis)
./build.ps1 docker-up

# Run API (reads ASPNETCORE_ENVIRONMENT from .env file)
./build.ps1 run

# Or run with explicit environment override
./build.ps1 local

# API available at http://localhost:8080
# Swagger UI available at http://localhost:8080/swagger
```

> **Note:** Swagger UI is enabled in `local` and `dev` environments.

**Environments:**
- `local` - Uses Docker Compose (localhost:27017, localhost:6379) - for local development
- `dev` - Uses real dev MongoDB/Redis - for development environment
- `stage` - Uses real stage MongoDB/Redis - for staging environment
- `prod` - Uses real prod MongoDB/Redis - for production environment

### Run with Docker Compose

```bash
# Start MongoDB + Redis only
./build.ps1 docker-up

# Start MongoDB + Redis + API (all in Docker)
./build.ps1 docker-up-api

# Stop all services
./build.ps1 docker-down

# API available at http://localhost:8080
# Health check: http://localhost:8080/health
```

---

## Commands

| Command | Description |
|---------|-------------|
| **API Commands** | |
| `./build.ps1 run` | Run API (uses environment from `.env` file) |
| `./build.ps1 local` | Run API in local mode (explicit override) |
| `./build.ps1 dev` | Run API in dev mode (explicit override) |
| `./build.ps1 stage` | Run API in stage mode (explicit override) |
| `./build.ps1 build` | Build the solution |
| `./build.ps1 test` | Run unit tests |
| `./build.ps1 test-coverage` | Run tests with coverage |
| `./build.ps1 test-api-e2e [-Env env]` | Run API E2E tests (default: local with Docker) |
| `./build.ps1 format` | Format code |
| `./build.ps1 clean` | Clean build artifacts |
| **Docker Commands** | |
| `./build.ps1 docker-up` | Start MongoDB + Redis only |
| `./build.ps1 docker-up-api` | Start MongoDB + Redis + API (all in Docker) |
| `./build.ps1 docker-down` | Stop all services |
| **Test Commands** | |
| `./build.ps1 test-all` | Run all tests (unit + E2E) |
| `./build.ps1 ultimate` | Run complete CI/CD pipeline |

---

## Project Structure

```
├── src/
│   ├── Domain/           # Core business entities (no dependencies)
│   ├── DTOs/             # Shared DTOs for all API versions (independent project)
│   ├── Application/      # Use cases, operations, validators
│   └── Infrastructure/   # Interface implementations
│       ├── Api/          # Controllers, middleware, configuration
│       ├── Cache/        # L1/L2 cache implementations
│       └── Persistence/  # Database, external services
│
├── tests/
│   ├── Tests/           # Unit tests (business logic + L1 cache)
│   └── Tests.Api.E2E/   # API E2E tests (MongoDB + Redis integration)
│
├── tools/
│   └── DatabaseMigrations/  # MongoDB index creation
│
├── docs/                 # Documentation
└── docker-compose.yml    # Local development and deployment
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [Why Clean Architecture](docs/01-why-clean-architecture.md) | N-Layer vs Clean Architecture, DIP explained |
| [Project Structure](docs/02-project-structure.md) | Folder organization and navigation |
| [API Versioning](docs/04-api-versioning.md) | Version strategy and DTO mapping |
| [Authentication & Authorization](docs/05-authentication-authorization.md) | API keys, user types, permissions |
| [Configuration](docs/06-configuration.md) | Environment settings and options |
| [Testing Strategy](docs/07-testing-strategy.md) | Unit tests vs E2E tests approach |
| [Docker Deployment](docs/08-docker-deployment.md) | Container setup and deployment |
| [Caching Strategy](docs/09-caching-strategy.md) | L1/L2 cache configuration and usage |
| [CancellationToken Best Practices](docs/10-cancellation-tokens.md) | Graceful cancellation and resource management |
| [Sealed Classes](docs/11-sealed-classes.md) | Performance optimization with sealed keyword |
| [E2E Testing](docs/14-E2E-testing.md) | Run E2E tests against any environment (local/dev/stage/prod) |
| [Kestrel Architecture](docs/19-kestrel-architecture.md) | Kestrel vs Tomcat, async I/O, thread safety by layer |
| [Graceful Shutdown](docs/20-graceful-shutdown.md) | SIGTERM handling, in-flight request completion, K8s integration |

---

## Editor Setup

This project uses **EditorConfig** for consistent code style.

### VS Code

Install the [EditorConfig for VS Code](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) extension.

### Key Settings

| Setting | Value |
|---------|-------|
| Line endings | LF (`\n`) |
| Indentation | 4 spaces (2 for JSON/YAML) |
| Charset | UTF-8 |
| Final newline | Yes |
| Trailing whitespace | Trimmed |

### Why LF Line Endings?

This project uses **LF** (`\n`) as the standard line ending for all files. LF is the modern cross-platform standard:

- ✅ **All OS support it** - Linux, macOS (native), Windows (since Windows 10)
- ✅ **Git friendly** - No noisy CRLF↔LF diff changes
- ✅ **Docker/CI compatible** - Shell scripts require LF
- ✅ **Smaller files** - 1 byte vs 2 bytes per line ending

Modern Windows tools handle LF perfectly: VS Code, Visual Studio 2017+, Notepad (Win10+), PowerShell, Git Bash.

---

## API Endpoints

### Orders (V1)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/orders` | List all orders |
| GET | `/api/v1/orders/{id}` | Get order by ID |
| POST | `/api/v1/orders` | Create order |
| PUT | `/api/v1/orders/{id}` | Update order status |
| DELETE | `/api/v1/orders/{id}` | Cancel order (soft delete) |

### Admin

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/admin/api-keys` | Create API key user |
| GET | `/api/v1/admin/api-keys` | List API key users |

### Health

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check (MongoDB, Redis) |

---

## Environment Configuration

The application uses a `.env` file for environment configuration. This file is loaded automatically at startup using [DotNetEnv](https://github.com/tonerdo/dotnet-env).

### Setup

```bash
# Copy the example file
cp .env.example .env

# Edit .env to configure your environment
```

### `.env` File Structure

```bash
# Application Configuration
ASPNETCORE_ENVIRONMENT=local    # local, dev, stage, prod, eu-prod, amr-prod

# Docker Compose Configuration
MONGO_USERNAME=username
MONGO_PASSWORD=password
MONGO_DATABASE=prescription_db
REDIS_PORT=6379
```

### How It Works

1. **API Application**: Loads `.env` file at startup via DotNetEnv, setting `ASPNETCORE_ENVIRONMENT` before configuration is built
2. **Docker Compose**: Uses `.env` file for service configuration (MongoDB credentials, Redis port)
3. **Tests**: Use explicit `ASPNETCORE_ENVIRONMENT` variable (via `./build.ps1 test-api-e2e`)

### Environment Guide

| Environment | Description |
|-------------|-------------|
| `local` | Uses Docker Compose (localhost:27017, localhost:6379) |
| `dev` | Uses real dev MongoDB/Redis |
| `stage` | Uses real stage MongoDB/Redis |
| `prod` | Uses real prod MongoDB/Redis |
| `eu-prod`, `amr-prod` | Regional production deployments |

See [E2E Testing](docs/14-E2E-testing.md) for more details.

---

## License

MIT

