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
- [Just](https://github.com/casey/just) - Cross-platform command runner

#### Installing Just on Windows

```bash
# Install using Scoop
scoop install just

# Add Git bin to PATH (Just requires sh.exe)
# Add C:\Program Files\Git\bin to your system PATH environment variable
```

### Run Locally (Development)

```bash
# Clone the repository
git clone <repository-url>
cd restful-api-service-dotnet

# Run with dev profile
just dev

# API available at http://localhost:5000
# Swagger UI available at http://localhost:5000/swagger
```

> **Note:** Swagger UI is only enabled in `dev` environment.

### Run with Docker (Production)

```bash
just docker-up    # Start services (MongoDB, Redis, API)
just docker-down  # Stop services

# API available at http://localhost:8080
# Health check: http://localhost:8080/health
```

---

## Commands

| Command | Description |
|---------|-------------|
| `just dev` | Run locally with dev profile |
| `just build` | Build the solution |
| `just test` | Run unit tests |
| `just test-coverage` | Run tests with coverage |
| `just test-e2e` | Run E2E tests |
| `just docker-e2e-down` | Stop E2E dependencies |
| `just format` | Format code |
| `just clean` | Clean build artifacts |
| `just docker-up` | Start Docker services |
| `just docker-down` | Stop Docker services |

---

## Project Structure

```
├── src/
│   ├── Entities/         # Core business entities (no dependencies)
│   ├── Application/      # Use cases, operations, validators
│   └── Adapters/         # Interface implementations
│       ├── Api/          # Controllers, middleware, configuration
│       └── Persistence/  # Database, cache, external services
│
├── tests/
│   ├── Tests/            # Unit tests
│   └── Tests.E2E/        # End-to-end integration tests
│
├── tools/
│   └── DatabaseMigrations/  # MongoDB index creation
│
├── docs/                 # Documentation
├── docker-compose.yml    # Production deployment
└── docker-compose.e2e.yml # E2E testing
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [Why Clean Architecture](docs/01-why-clean-architecture.md) | N-Layer vs Clean Architecture comparison |
| [Project Structure](docs/02-project-structure.md) | Folder organization and navigation |
| [API Versioning](docs/04-api-versioning.md) | Version strategy and DTO mapping |
| [Authentication & Authorization](docs/05-authentication-authorization.md) | API keys, user types, permissions |
| [Configuration](docs/06-configuration.md) | Environment settings and options |
| [Testing Strategy](docs/07-testing-strategy.md) | Unit tests vs E2E tests approach |
| [Docker Deployment](docs/08-docker-deployment.md) | Container setup and deployment |
| [Caching Strategy](docs/09-caching-strategy.md) | L1/L2 cache configuration and usage |

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

## Environment Variables

```bash
ASPNETCORE_ENVIRONMENT=prod           # dev, stage, eu-stage, amr-stage, e2e, prod, eu-prod, amr-prod
MongoDB__ConnectionString=mongodb://host:27017
Redis__ConnectionString=redis:6379
RootAdmin__InitialApiKey=your-key    # Change immediately!
```

---

## License

MIT

