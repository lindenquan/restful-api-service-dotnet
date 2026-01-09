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
        await ApplySchemaValidationAsync();
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
        await CreateIndexAsync("prescriptions", "prescriptionNumber", unique: true, sparse: true);
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

    private async Task CreateIndexAsync(string collectionName, string fieldName, bool unique = false, bool sparse = false)
    {
        var collection = _database.GetCollection<BsonDocument>(collectionName);
        var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending(fieldName);
        var indexOptions = new CreateIndexOptions
        {
            Unique = unique,
            Sparse = sparse,
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

    private async Task ApplySchemaValidationAsync()
    {
        Console.WriteLine("[3/4] Applying schema validation...");

        await ApplyPatientSchemaAsync();
        await ApplyPrescriptionSchemaAsync();
        await ApplyOrderSchemaAsync();
        await ApplyUserSchemaAsync();
    }

    private async Task ApplyPatientSchemaAsync()
    {
        var schema = new BsonDocument
        {
            ["bsonType"] = "object",
            ["required"] = new BsonArray { "firstName", "lastName", "email", "dateOfBirth" },
            ["properties"] = new BsonDocument
            {
                ["firstName"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = 100,
                    ["description"] = "First name is required (1-100 characters)"
                },
                ["lastName"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = 100,
                    ["description"] = "Last name is required (1-100 characters)"
                },
                ["email"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["maxLength"] = 200,
                    ["description"] = "Valid email address required"
                },
                ["phone"] = new BsonDocument
                {
                    ["bsonType"] = new BsonArray { "string", "null" },
                    ["maxLength"] = 20
                },
                ["dateOfBirth"] = new BsonDocument
                {
                    ["bsonType"] = "date",
                    ["description"] = "Date of birth is required"
                }
            }
        };

        await ApplySchemaToCollectionAsync("patients", schema);
    }

    private async Task ApplyPrescriptionSchemaAsync()
    {
        var schema = new BsonDocument
        {
            ["bsonType"] = "object",
            ["required"] = new BsonArray { "patientId", "medicationName", "dosage", "frequency", "quantity", "prescriberName", "expiryDate" },
            ["properties"] = new BsonDocument
            {
                ["patientId"] = new BsonDocument
                {
                    ["bsonType"] = "binData",
                    ["description"] = "Patient ID (UUID) is required"
                },
                ["medicationName"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = 200,
                    ["description"] = "Medication name is required (1-200 characters)"
                },
                ["dosage"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = 50,
                    ["description"] = "Dosage is required (1-50 characters)"
                },
                ["frequency"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = 100,
                    ["description"] = "Frequency is required (1-100 characters)"
                },
                ["quantity"] = new BsonDocument
                {
                    ["bsonType"] = "int",
                    ["minimum"] = 1,
                    ["maximum"] = 1000,
                    ["description"] = "Quantity must be between 1 and 1000"
                },
                ["refillsRemaining"] = new BsonDocument
                {
                    ["bsonType"] = "int",
                    ["minimum"] = 0,
                    ["maximum"] = 12,
                    ["description"] = "Refills remaining must be between 0 and 12"
                },
                ["prescriberName"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["minLength"] = 1,
                    ["maxLength"] = 200,
                    ["description"] = "Prescriber name is required (1-200 characters)"
                },
                ["expiryDate"] = new BsonDocument
                {
                    ["bsonType"] = "date",
                    ["description"] = "Expiry date is required"
                },
                ["instructions"] = new BsonDocument
                {
                    ["bsonType"] = new BsonArray { "string", "null" },
                    ["maxLength"] = 1000
                }
            }
        };

        await ApplySchemaToCollectionAsync("prescriptions", schema);
    }

    private async Task ApplyOrderSchemaAsync()
    {
        var schema = new BsonDocument
        {
            ["bsonType"] = "object",
            ["required"] = new BsonArray { "patientId", "prescriptionId", "orderDate", "status" },
            ["properties"] = new BsonDocument
            {
                ["patientId"] = new BsonDocument
                {
                    ["bsonType"] = "binData",
                    ["description"] = "Patient ID (UUID) is required"
                },
                ["prescriptionId"] = new BsonDocument
                {
                    ["bsonType"] = "binData",
                    ["description"] = "Prescription ID (UUID) is required"
                },
                ["orderDate"] = new BsonDocument
                {
                    ["bsonType"] = "date",
                    ["description"] = "Order date is required"
                },
                ["status"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["enum"] = new BsonArray { "Pending", "Processing", "Ready", "Completed", "Cancelled" },
                    ["description"] = "Order status (Pending, Processing, Ready, Completed, Cancelled)"
                },
                ["notes"] = new BsonDocument
                {
                    ["bsonType"] = new BsonArray { "string", "null" },
                    ["maxLength"] = 500
                }
            }
        };

        await ApplySchemaToCollectionAsync("orders", schema);
    }

    private async Task ApplyUserSchemaAsync()
    {
        var schema = new BsonDocument
        {
            ["bsonType"] = "object",
            ["required"] = new BsonArray { "email", "apiKeyHash", "userType" },
            ["properties"] = new BsonDocument
            {
                ["email"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["maxLength"] = 200,
                    ["description"] = "Email is required"
                },
                ["apiKeyHash"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["description"] = "API key hash is required"
                },
                ["userType"] = new BsonDocument
                {
                    ["bsonType"] = "string",
                    ["enum"] = new BsonArray { "Regular", "Admin" },
                    ["description"] = "User type (Regular or Admin)"
                },
                ["isActive"] = new BsonDocument
                {
                    ["bsonType"] = "bool"
                }
            }
        };

        await ApplySchemaToCollectionAsync("users", schema);
    }

    private async Task ApplySchemaToCollectionAsync(string collectionName, BsonDocument schema)
    {
        try
        {
            var command = new BsonDocument
            {
                ["collMod"] = collectionName,
                ["validator"] = new BsonDocument { ["$jsonSchema"] = schema },
                ["validationLevel"] = "strict",
                ["validationAction"] = "error"
            };

            await _database.RunCommandAsync<BsonDocument>(command);
            Console.WriteLine($"  ✓ Schema validation applied: {collectionName}");
        }
        catch (MongoCommandException ex)
        {
            Console.WriteLine($"  ⚠ Schema validation failed for {collectionName}: {ex.Message}");
        }
    }

    private async Task CreateMigrationHistoryAsync()
    {
        Console.WriteLine("[4/4] Recording migration...");

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

