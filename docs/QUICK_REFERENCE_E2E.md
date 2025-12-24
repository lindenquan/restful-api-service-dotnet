# E2E Testing Quick Reference

## 🚀 Quick Commands

### API E2E Tests

```bash
# Local (Docker) - Default
just test-api-e2e

# Dev environment
just test-api-e2e dev

# Stage environment
just test-api-e2e stage
```

### Web E2E Tests

```bash
# Local (Docker) - Default
just web-test-e2e

# Dev environment
just web-test-e2e dev

# Stage environment
just web-test-e2e stage
```

## 🎯 What Happens

### Local Environment (`just test-api-e2e`)

```
1. 🚀 Start Docker (MongoDB + Redis)
2. ⏳ Wait 10 seconds
3. 🧪 Run tests with ASPNETCORE_ENVIRONMENT=local
4. 🛑 Stop Docker
```

### Other Environments (`just test-api-e2e dev`)

```
1. 🧪 Run tests with ASPNETCORE_ENVIRONMENT=dev
   (Uses real dev servers from appsettings.dev.json)
```

## 📋 Prerequisites

### For Local Environment
- ✅ Docker installed and running
- ✅ Ports available: 27017 (MongoDB), 6379 (Redis), 5000 (API)

### For Other Environments
- ✅ Network access to dev/stage/prod servers
- ✅ Valid connection strings in config files
- ✅ VPN/authentication if required

## 🔧 Configuration Files

```
config/
├── appsettings.json           # Base config
├── appsettings.local.json     # localhost:27017, localhost:6379
├── appsettings.dev.json       # dev servers
├── appsettings.stage.json     # stage servers
└── appsettings.prod.json      # prod servers
```

## 💡 Tips

### Run specific test class

```bash
# Local
just docker-up
ASPNETCORE_ENVIRONMENT=local dotnet test tests/Tests.Api.E2E --filter "OrdersApiE2ETests"
just docker-down

# Dev
ASPNETCORE_ENVIRONMENT=dev dotnet test tests/Tests.Api.E2E --filter "OrdersApiE2ETests"
```

### Keep Docker running for multiple test runs

```bash
# Start Docker once
just docker-up

# Run tests multiple times
ASPNETCORE_ENVIRONMENT=local dotnet test tests/Tests.Api.E2E
ASPNETCORE_ENVIRONMENT=local dotnet test tests/Tests.Api.E2E --filter "OrdersApiE2ETests"

# Stop Docker when done
just docker-down
```

### Debug tests

```bash
# Start Docker
just docker-up

# Run with detailed output
ASPNETCORE_ENVIRONMENT=local dotnet test tests/Tests.Api.E2E --verbosity detailed

# Or run in IDE with debugger
# (Set ASPNETCORE_ENVIRONMENT=local in launch settings)

# Stop Docker
just docker-down
```

## 🚨 Troubleshooting

### "API is not accessible"
- Check Docker is running: `docker ps`
- Check ports: `netstat -an | findstr "27017 6379 5000"`
- Restart Docker: `just docker-down && just docker-up-api`

### "Connection refused" (MongoDB/Redis)
- Verify Docker containers: `docker compose ps`
- Check logs: `docker compose logs mongodb redis`
- Restart: `just docker-down && just docker-up`

### Tests fail on dev/stage
- Verify network access to servers
- Check VPN connection
- Verify credentials in config files
- Test connection: `curl http://dev-api.example.com/health`

## 📚 More Information

- [E2E Testing Environments](E2E_TESTING_ENVIRONMENTS.md) - Complete guide
- [Web E2E Testing](WEB_E2E_TESTING.md) - Playwright details
- [Testing Strategy](07-testing-strategy.md) - Overall approach

