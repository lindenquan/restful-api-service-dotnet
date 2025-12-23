namespace Adapters.Persistence.Configuration;

/// <summary>
/// MongoDB configuration settings.
/// </summary>
public class MongoDbSettings
{
    public const string SectionName = "MongoDB";

    /// <summary>
    /// MongoDB connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    /// <summary>
    /// Database name.
    /// </summary>
    public string DatabaseName { get; set; } = "prescription_orders";

    /// <summary>
    /// Patients collection name.
    /// </summary>
    public string PatientsCollection { get; set; } = "patients";

    /// <summary>
    /// Prescriptions collection name.
    /// </summary>
    public string PrescriptionsCollection { get; set; } = "prescriptions";

    /// <summary>
    /// Orders collection name.
    /// </summary>
    public string OrdersCollection { get; set; } = "orders";

    /// <summary>
    /// Users (API Key) collection name.
    /// </summary>
    public string UsersCollection { get; set; } = "users";
}

