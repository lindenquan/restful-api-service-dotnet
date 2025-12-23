using DatabaseMigrations;
using Microsoft.Extensions.Configuration;

Console.WriteLine("===========================================");
Console.WriteLine("  MongoDB Database Migration Tool");
Console.WriteLine("===========================================");
Console.WriteLine();

// Build configuration
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
Console.WriteLine($"Environment: {environment}");

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Get MongoDB settings
var connectionString = configuration["MongoDB:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDB:ConnectionString is required");
var databaseName = configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDB:DatabaseName is required");

Console.WriteLine($"Database: {databaseName}");
Console.WriteLine();

// Run migrations
var migrator = new MongoDbMigrator(connectionString, databaseName);
await migrator.RunMigrationsAsync();

Console.WriteLine();
Console.WriteLine("Migration completed successfully!");
Console.WriteLine("===========================================");
