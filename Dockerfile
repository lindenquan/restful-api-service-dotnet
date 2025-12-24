# Dockerfile for Prescription Order API Service
# Builds a containerized version of the API for Docker Compose and deployment

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY RestfulApiService.slnx .
COPY src/Entities/Entities.csproj src/Entities/
COPY src/DTOs/DTOs.csproj src/DTOs/
COPY src/Application/Application.csproj src/Application/
COPY src/Adapters/Adapters.csproj src/Adapters/

# Restore dependencies
RUN dotnet restore src/Adapters/Adapters.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/Adapters/Adapters.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Copy configuration files (appsettings.*.json)
COPY config/ ./config/

EXPOSE 8080

ENTRYPOINT ["dotnet", "Adapters.dll"]

