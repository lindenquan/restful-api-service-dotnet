# Docker Deployment

## Overview

The project includes Docker configuration for:

| File | Purpose |
|------|---------|
| `docker-compose.yml` | E2E testing environment |
| `src/Api/Dockerfile` | API container image |
| `tools/DatabaseMigrations/Dockerfile` | Migration runner image |

---

## Production Stack

```
┌─────────────────────────────────────────────────────────────┐
│                    docker-compose.yml                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐              │
│  │  MongoDB │    │   Redis  │    │   API    │              │
│  │  :27017  │◄───│  :6379   │◄───│  :8080   │              │
│  └──────────┘    └──────────┘    └──────────┘              │
│        ▲                               │                    │
│        │         ┌──────────┐          │                    │
│        └─────────│Migrations│◄─────────┘                    │
│                  │ (init)   │                               │
│                  └──────────┘                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Running Production Stack

```bash
# Start all services
docker compose up -d

# View logs
docker compose logs -f api

# Stop all services
docker compose down

# Stop and remove volumes (clean slate)
docker compose down -v
```

---


## Building Images

```bash
# Build API image
docker build -t prescription-api -f src/Api/Dockerfile .

# Build migrations image
docker build -t prescription-migrations -f tools/DatabaseMigrations/Dockerfile .
```

---

## E2E Testing Stack

Separate compose file for E2E tests:

```bash
# Start E2E dependencies
docker-compose -f docker-compose.e2e.yml up -d

# Run tests
dotnet test tests/Tests.E2E

# Clean up
docker-compose -f docker-compose.e2e.yml down -v
```

---

## Health Check

The API exposes a health endpoint for Docker health checks:

```
GET /health
```

Response:
```json
{
  "status": "Healthy",
  "entries": {
    "self": { "status": "Healthy" },
    "mongodb": { "status": "Healthy" },
    "redis": { "status": "Healthy" }
  }
}
```

---

## Environment Variables for Production

```bash
# Required
ASPNETCORE_ENVIRONMENT=prod
MongoDB__ConnectionString=mongodb://user:pass@host:27017
Redis__ConnectionString=redis-host:6379

# Security - MUST change
RootAdmin__InitialApiKey=your-secure-key

```

---

## Deployment Checklist

- [ ] Change `RootAdmin__InitialApiKey` to secure value
- [ ] Configure CORS `AllowedOrigins` for your domains
- [ ] Set up TLS certificates for HTTPS
- [ ] Configure MongoDB authentication
- [ ] Configure Redis password
- [ ] Set up log aggregation
- [ ] Configure monitoring/alerting
- [ ] Set up backup for MongoDB volumes
