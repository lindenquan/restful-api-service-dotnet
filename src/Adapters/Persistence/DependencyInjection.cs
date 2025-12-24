using Adapters.Persistence.Configuration;
using Adapters.Persistence.Repositories;
using Adapters.Persistence.Security;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Adapters.Persistence;

/// <summary>
/// Extension methods for registering Persistence services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Add persistence services with MongoDB as the database provider.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        MongoDbSettings mongoDbSettings)
    {
        // Register MongoDB
        services.AddMongoDb(mongoDbSettings);

        // Security services
        services.AddSingleton<IApiKeyGenerator, ApiKeyGeneratorService>();

        return services;
    }

    /// <summary>
    /// Add MongoDB database provider.
    /// </summary>
    private static IServiceCollection AddMongoDb(
        this IServiceCollection services,
        MongoDbSettings settings)
    {
        // Configure BSON serialization conventions (must be done before any MongoDB operations)
        ConfigureBsonSerialization();

        // Register settings
        services.AddSingleton(settings);

        // Register MongoDB client as singleton
        // Use GetConnectionString() to inject username/password if provided
        services.AddSingleton<IMongoClient>(_ =>
            new MongoClient(settings.GetConnectionString()));

        // Register Unit of Work as scoped
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        return services;
    }

    private static bool _bsonConfigured = false;
    private static readonly object _bsonLock = new();

    /// <summary>
    /// Configure BSON serialization conventions for MongoDB.
    /// Uses camelCase for property names to match MongoDB conventions.
    /// </summary>
    private static void ConfigureBsonSerialization()
    {
        lock (_bsonLock)
        {
            if (_bsonConfigured)
                return;

            // Register camelCase convention for all types
            var conventionPack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new EnumRepresentationConvention(BsonType.String)
            };

            ConventionRegistry.Register("CamelCaseConventions", conventionPack, _ => true);

            // Configure DateTime serialization to use UTC
            BsonSerializer.RegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));

            _bsonConfigured = true;
        }
    }
}

