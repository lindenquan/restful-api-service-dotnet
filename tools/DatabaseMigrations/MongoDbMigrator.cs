using MongoDB.Bson;
using MongoDB.Driver;

namespace DatabaseMigrations;

/// <summary>
/// Handles MongoDB database migrations: collections, indexes, and schema validation.
/// </summary>
public class MongoDbMigrator
{
    private readonly IMongoDatabase _database;
    private readonly IMongoClient _client;

    public MongoDbMigrator(string connectionString, string databaseName)
    {
        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase(databaseName);
    }

    public async Task RunMigrationsAsync()
    {
        Console.WriteLine("Starting migrations...");
        Console.WriteLine();

        // Run all migrations in order
        await CreateCollectionsAsync();
        await CreateIndexesAsync();
        await CreateMigrationHistoryAsync();

        Console.WriteLine();
        Console.WriteLine("All migrations completed.");
    }

    private async Task CreateCollectionsAsync()
    {
        Console.WriteLine("[1/3] Creating collections...");

        // Collections:
        // - patients: Patient entity (customer/patient data)
        // - users: User entity (API authentication users)
        // - prescriptions: Prescription entity
        // - orders: PrescriptionOrder entity
        // - migrationHistory: Migration tracking
        var collections = new[]
        {
            "patients",
            "users",
            "prescriptions",
            "orders",
            "migrationHistory"
        };

        var existingCollections = await (await _database.ListCollectionNamesAsync()).ToListAsync();

        foreach (var collectionName in collections)
        {
            if (!existingCollections.Contains(collectionName))
            {
                await _database.CreateCollectionAsync(collectionName);
                Console.WriteLine($"  ✓ Created collection: {collectionName}");
            }
            else
            {
                Console.WriteLine($"  - Collection exists: {collectionName}");
            }
        }
    }

    private async Task CreateIndexesAsync()
    {
        Console.WriteLine("[2/3] Creating indexes...");

        // Patients collection indexes (customer/patient data)
        await CreateIndexAsync("patients", "email", unique: true);
        await CreateIndexAsync("patients", "metadata.isDeleted");

        // Users collection indexes (API authentication users)
        await CreateIndexAsync("users", "apiKeyHash", unique: true);
        await CreateIndexAsync("users", "email", unique: true);
        await CreateIndexAsync("users", "metadata.isDeleted");
        await CreateIndexAsync("users", "isActive");

        // Prescriptions collection indexes
        await CreateIndexAsync("prescriptions", "patientId");
        await CreateIndexAsync("prescriptions", "prescriptionNumber", unique: true);
        await CreateIndexAsync("prescriptions", "metadata.isDeleted");

        // Orders collection indexes
        await CreateIndexAsync("orders", "patientId");
        await CreateIndexAsync("orders", "prescriptionId");
        await CreateIndexAsync("orders", "status");
        await CreateIndexAsync("orders", "metadata.isDeleted");
        await CreateCompoundIndexAsync("orders", new[] { "patientId", "status" });

        // Migration history index
        await CreateIndexAsync("migrationHistory", "version", unique: true);
    }

    private async Task CreateIndexAsync(string collectionName, string fieldName, bool unique = false)
    {
        var collection = _database.GetCollection<BsonDocument>(collectionName);
        var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending(fieldName);
        var indexOptions = new CreateIndexOptions
        {
            Unique = unique,
            Background = true,
            Name = $"idx_{fieldName}"
        };

        try
        {
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(indexKeys, indexOptions));
            Console.WriteLine($"  ✓ Index on {collectionName}.{fieldName}{(unique ? " (unique)" : "")}");
        }
        catch (MongoCommandException ex) when (ex.Code == 85 || ex.Code == 86)
        {
            // Index already exists with same or different options
            Console.WriteLine($"  - Index exists: {collectionName}.{fieldName}");
        }
    }

    private async Task CreateCompoundIndexAsync(string collectionName, string[] fieldNames)
    {
        var collection = _database.GetCollection<BsonDocument>(collectionName);
        var indexKeysBuilder = Builders<BsonDocument>.IndexKeys;
        var indexKeys = indexKeysBuilder.Combine(
            fieldNames.Select(f => indexKeysBuilder.Ascending(f)));
        var indexName = $"idx_{string.Join("_", fieldNames)}";
        var indexOptions = new CreateIndexOptions
        {
            Background = true,
            Name = indexName
        };

        try
        {
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(indexKeys, indexOptions));
            Console.WriteLine($"  ✓ Compound index on {collectionName}.({string.Join(", ", fieldNames)})");
        }
        catch (MongoCommandException ex) when (ex.Code == 85 || ex.Code == 86)
        {
            Console.WriteLine($"  - Compound index exists: {collectionName}.({string.Join(", ", fieldNames)})");
        }
    }

    private async Task CreateMigrationHistoryAsync()
    {
        Console.WriteLine("[3/3] Recording migration...");

        var collection = _database.GetCollection<BsonDocument>("migrationHistory");
        var version = "1.0.0";

        var existingMigration = await collection
            .Find(Builders<BsonDocument>.Filter.Eq("version", version))
            .FirstOrDefaultAsync();

        if (existingMigration == null)
        {
            var migration = new BsonDocument
            {
                { "version", version },
                { "description", "Initial database setup - collections and indexes" },
                { "appliedAt", DateTime.UtcNow },
                { "appliedBy", Environment.MachineName }
            };

            await collection.InsertOneAsync(migration);
            Console.WriteLine($"  ✓ Recorded migration version: {version}");
        }
        else
        {
            Console.WriteLine($"  - Migration already recorded: {version}");
        }
    }
}

