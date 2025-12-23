# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files
COPY *.slnx .
COPY src/Entities/Entities.csproj src/Entities/
COPY src/Application/Application.csproj src/Application/
COPY src/Adapters/Adapters.csproj src/Adapters/

# Restore dependencies
RUN dotnet restore src/Adapters/Adapters.csproj

# Copy all source code
COPY src/ src/

# Build and publish
WORKDIR /src/src/Adapters
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Copy configuration files from solution-level config folder
COPY config/ /app/config/

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Run the API
ENTRYPOINT ["dotnet", "Adapters.dll"]

