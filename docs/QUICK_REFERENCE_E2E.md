# E2E Testing Quick Reference

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

## 🎯 What Happens

### Local Environment (`./build.ps1 test-api-e2e`)

```
1. 🚀 Start Docker (MongoDB + Redis)
2. ⏳ Wait 10 seconds
3. 🧪 Run tests with ASPNETCORE_ENVIRONMENT=local
4. 🛑 Stop Docker
```

### Other Environments (`./build.ps1 test-api-e2e -Env dev`)

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

## 📚 More Information

- [E2E Testing Environments](E2E_TESTING_ENVIRONMENTS.md) - Complete guide
- [Testing Strategy](07-testing-strategy.md) - Overall approach

