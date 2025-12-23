# Run in dev mode (Swagger UI enabled)
dev:
    ASPNETCORE_ENVIRONMENT="dev" dotnet run --project src/Adapters

# Run in stage mode
stage:
   ASPNETCORE_ENVIRONMENT="stage" dotnet run --project src/Adapters

# Run in e2e mode
e2e:
   ASPNETCORE_ENVIRONMENT="e2e" dotnet run --project src/Adapters

# Build solution
build:
    dotnet build

# Run unit tests
test:
    dotnet test tests/Tests --verbosity minimal

# Run unit tests with coverage
test-coverage:
    dotnet test tests/Tests --collect:"XPlat Code Coverage"

# Run API E2E tests (requires MongoDB and Redis via docker-compose)
test-api-e2e:
    dotnet test tests/Tests.Api.E2E --verbosity minimal

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

# Web App: Clean build artifacts
web-clean:
    dotnet clean src/Web
    rm -rf src/Web/bin src/Web/obj

# Web App: Build
web-build:
    dotnet build src/Web

# Web App: Run unit tests
web-test:
    dotnet test tests/Tests.Web --verbosity minimal

# Web App: Run in development mode
web-dev:
    dotnet watch run --project src/Web

# Web App: Build and run from dist folder (preview mode)
web-preview:
    dotnet publish src/Web -c Release -o src/Web/dist
    dotnet serve -d src/Web/dist/wwwroot -p 5002

# Docker: Build AWS Lambda image
docker-build-lambda:
    docker build -f Dockerfile.lambda -t prescription-api-lambda:latest .

# Docker: Build AWS EKS image
docker-build-eks:
    docker build -f Dockerfile.eks -t prescription-api-eks:latest .

# Docker: Build all images
docker-build-all: docker-build-lambda docker-build-eks
    @echo "All Docker images built successfully"

# Ultimate: Run complete CI/CD pipeline
ultimate:
    @echo "========================================="
    @echo "Starting Complete CI/CD Pipeline"
    @echo "========================================="
    @echo ""
    @echo "========================================="
    @echo "PART 1: API Service Testing"
    @echo "========================================="
    @echo ""
    @echo "Step 1.1: Clean API build artifacts..."
    just clean
    @echo ""
    @echo "Step 1.2: Build API solution..."
    just build
    @echo ""
    @echo "Step 1.3: Run API unit tests..."
    just test
    @echo ""
    @echo "Step 1.4: Start Docker Compose services..."
    just docker-up
    @echo "Waiting 20 seconds for services to start..."
    sleep 20
    @echo ""
    @echo "Step 1.5: Run API E2E tests..."
    just test-api-e2e || (just docker-down && exit 1)
    @echo ""
    @echo "Step 1.6: Stop Docker Compose services..."
    just docker-down
    @echo ""
    @echo "========================================="
    @echo "PART 2: Web Client Testing"
    @echo "========================================="
    @echo ""
    @echo "Step 2.1: Clean Web build artifacts..."
    just web-clean
    @echo ""
    @echo "Step 2.2: Build Web client..."
    just web-build
    @echo ""
    @echo "Step 2.3: Run Web unit tests..."
    just web-test
    @echo ""
    @echo "========================================="
    @echo "✅ Complete CI/CD Pipeline Successful!"
    @echo "========================================="

