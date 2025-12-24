namespace Adapters.Persistence.Configuration;

/// <summary>
/// MongoDB configuration settings.
/// </summary>
public class MongoDbSettings
{
    public const string SectionName = "MongoDB";

    /// <summary>
    /// MongoDB connection string.
    /// If Username and Password are provided, they will be injected into the connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    /// <summary>
    /// MongoDB username (optional). If provided, will be injected into ConnectionString.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// MongoDB password (optional). If provided, will be injected into ConnectionString.
    /// </summary>
    public string? Password { get; set; }

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

    /// <summary>
    /// Gets the final connection string with username/password injected if provided.
    /// </summary>
    public string GetConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            return ConnectionString;
        }

        // Inject username and password into connection string
        // Supports both mongodb:// and mongodb+srv:// protocols
        var uri = new Uri(ConnectionString);
        var protocol = uri.Scheme; // "mongodb" or "mongodb+srv"
        var hostAndPath = ConnectionString.Replace($"{protocol}://", "");

        return $"{protocol}://{Uri.EscapeDataString(Username)}:{Uri.EscapeDataString(Password)}@{hostAndPath}";
    }
}

