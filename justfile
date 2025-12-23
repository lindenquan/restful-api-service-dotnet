# Run in dev mode (Swagger UI enabled)
dev:
    ASPNETCORE_ENVIRONMENT="dev" dotnet run --project src/Adapters

# Run in stage mode
stage:
   ASPNETCORE_ENVIRONMENT="stage" dotnet run --project src/Adapters

# Run in e2e mode
e2e:
   ASPNETCORE_ENVIRONMENT="e2e"dotnet run --project src/Adapters

# Build solution
build:
    dotnet build

# Run unit tests
test:
    dotnet test tests/Tests --verbosity minimal

# Run unit tests with coverage
test-coverage:
    dotnet test tests/Tests --collect:"XPlat Code Coverage"

# Run E2E tests
test-e2e:
    dotnet test tests/Tests.E2E --verbosity minimal

# Format code
format:
    dotnet format

# Clean build artifacts
clean:
    dotnet clean
    rm -rf src/*/bin src/*/obj tests/*/bin tests/*/obj

# Restore packages
restore:
    dotnet restore

# Docker: start all services
docker-up:
    docker compose up -d

# Docker: stop all services
docker-down:
    docker compose down

