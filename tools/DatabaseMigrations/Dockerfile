# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy just the migration project files
COPY tools/DatabaseMigrations/DatabaseMigrations.csproj tools/DatabaseMigrations/
RUN dotnet restore tools/DatabaseMigrations/DatabaseMigrations.csproj

# Copy migration source code
COPY tools/DatabaseMigrations/ tools/DatabaseMigrations/

# Build the migration tool
WORKDIR /src/tools/DatabaseMigrations
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Run migrations
ENTRYPOINT ["dotnet", "DatabaseMigrations.dll"]

