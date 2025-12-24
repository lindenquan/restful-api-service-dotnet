# Run API (environment is loaded from .env file)
# To change environment, edit ASPNETCORE_ENVIRONMENT in .env file
run:
    dotnet run --project src/Adapters

# Run in local mode (uses Docker Compose for MongoDB/Redis)
local:
    dotnet run --project src/Adapters --environment local

# Run in dev mode (uses real dev MongoDB/Redis)
dev:
    dotnet run --project src/Adapters --environment dev

# Run in stage mode (uses real stage MongoDB/Redis)
stage:
    dotnet run --project src/Adapters --environment stage

# Build solution
build:
    dotnet build

# Run unit tests
test:
    dotnet test tests/Tests --verbosity minimal

# Run unit tests with coverage
test-coverage:
    dotnet test tests/Tests --collect:"XPlat Code Coverage"

# Run API E2E tests with environment support
# Usage: just test-api-e2e [env]
# Examples:
#   just test-api-e2e          # Uses local (starts Docker automatically)
#   just test-api-e2e local    # Uses local (starts Docker automatically)
#   just test-api-e2e dev      # Uses dev (no Docker, uses real services)
# Note: Tests require ASPNETCORE_ENVIRONMENT to be set explicitly (not from .env)
test-api-e2e env="local":
    set -e; \
    if [ "{{env}}" = "local" ]; then \
    echo "🚀 Starting Docker services (MongoDB + Redis)..."; \
    docker compose up -d mongodb redis; \
    echo "⏳ Waiting for services to be ready..."; \
    sleep 10; \
    fi; \
    echo "🚀 Running API E2E tests (ASPNETCORE_ENVIRONMENT={{env}})..."; \
    ASPNETCORE_ENVIRONMENT={{env}} dotnet test tests/Tests.Api.E2E --verbosity minimal; \
    if [ "{{env}}" = "local" ]; then \
    echo "🛑 Stopping Docker services..."; \
    docker compose down; \
    fi

# Run all tests (unit + API E2E)
test-all:
    echo "========================================="
    echo "Running ALL Tests"
    echo "========================================="
    echo ""
    echo "Step 1: API Unit Tests..."
    just test
    echo ""
    echo "Step 2: API E2E Tests..."
    just test-api-e2e
    echo ""
    echo "========================================="
    echo "✅ All Tests Passed!"
    echo "========================================="

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

# Docker: Start MongoDB + Redis only (for API E2E tests)
docker-up:
    echo "🚀 Starting MongoDB + Redis..."
    docker compose up -d mongodb redis
    sleep 10s

# Docker: Start MongoDB + Redis + API (all services)
docker-up-api:
    echo "🚀 Starting MongoDB + Redis + API..."
    docker compose up -d --build
    sleep 20s

# Docker: Stop all services
docker-down:
    echo "🛑 Stopping services..."
    docker compose down

# Docker: Build AWS Lambda image
docker-build-lambda:
    docker build -f Dockerfile.lambda -t prescription-api-lambda:latest .

# Docker: Build AWS EKS image
docker-build-eks:
    docker build -f Dockerfile.eks -t prescription-api-eks:latest .

# Docker: Build all images
docker-build-all: docker-build-lambda docker-build-eks
    echo "All Docker images built successfully"

# Ultimate: Run complete CI/CD pipeline (all tests including E2E)
ultimate:
    echo "========================================="
    echo "Starting Complete CI/CD Pipeline"
    echo "========================================="
    echo ""
    echo "Step 1: Clean build artifacts..."
    just clean
    echo ""
    echo "Step 2: Build solution..."
    just build
    echo ""
    echo "Step 3: Run all tests..."
    just test-all
    echo ""
    echo "========================================="
    echo "✅ Complete CI/CD Pipeline Successful!"
    echo "========================================="
    echo ""
    echo "Summary:"
    echo "  - API Unit Tests: ✅"
    echo "  - API E2E Tests: ✅"
