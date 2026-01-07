# Docker Deployment

## Overview

The project includes Docker configuration for:

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Local development and E2E testing |
| `Dockerfile` | API container image |
| `Dockerfile.lambda` | AWS Lambda deployment image |
| `Dockerfile.eks` | AWS EKS deployment image |

---

## Production Stack

```
┌─────────────────────────────────────────────────────────────┐
│                    docker-compose.yml                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐               │
│  │  MongoDB │    │   Redis  │    │   API    │               │
│  │  :27017  │◄───│  :6379   │◄───│  :8080   │               │
│  └──────────┘    └──────────┘    └──────────┘               │
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
docker build -t prescription-api -f src/Infrastructure/Api/Dockerfile .

# Build Lambda image
./build.ps1 docker-build-lambda

# Build EKS image
./build.ps1 docker-build-eks

# Build all images
./build.ps1 docker-build-all
```

---

## E2E Testing Stack

Uses the same docker-compose.yml for E2E tests:

```bash
# Start MongoDB and Redis
./build.ps1 docker-up

# Run API E2E tests
./build.ps1 test-api-e2e

# Or run tests against dev/stage environments
./build.ps1 test-api-e2e -Env dev

# Clean up
./build.ps1 docker-down
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
