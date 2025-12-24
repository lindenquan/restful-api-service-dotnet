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
var username = configuration["MongoDB:Username"];
var password = configuration["MongoDB:Password"];
var databaseName = configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDB:DatabaseName is required");

// Inject username/password into connection string if provided
if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
{
    var uri = new Uri(connectionString);
    var protocol = uri.Scheme; // "mongodb" or "mongodb+srv"
    var hostAndPath = connectionString.Replace($"{protocol}://", "");
    connectionString = $"{protocol}://{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}@{hostAndPath}";
}

Console.WriteLine($"Database: {databaseName}");
Console.WriteLine();

// Run migrations
var migrator = new MongoDbMigrator(connectionString, databaseName);
await migrator.RunMigrationsAsync();

Console.WriteLine();
Console.WriteLine("Migration completed successfully!");
Console.WriteLine("===========================================");
