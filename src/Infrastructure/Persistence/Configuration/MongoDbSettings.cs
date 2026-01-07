namespace Infrastructure.Persistence.Configuration;

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

    // =============================================================================
    // Connection Pool Settings
    // =============================================================================
    // Pool Sizing Formula: MaxPoolSize = (PodCpuCores × 20) + HeadroomBuffer
    // Memory: Each connection uses ~1-5 MB
    // K8s: Total = MaxPoolSize × NumberOfPods (must be < MongoDB cluster limit)
    // =============================================================================

    /// <summary>
    /// Maximum number of connections in the connection pool per pod.
    /// Default: 25 (suitable for 1-2 vCPU pods).
    /// Scale formula: (vCPU cores × 20) + 10-20% headroom.
    /// </summary>
    public int MaxPoolSize { get; set; } = 25;

    /// <summary>
    /// Minimum number of connections to keep warm in the pool.
    /// Reduces cold start latency for new requests.
    /// </summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>
    /// Maximum time (seconds) a connection can remain idle before being closed.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int MaxIdleTimeSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum time (seconds) to wait for a connection to become available.
    /// Returns error if no connection available within this time.
    /// </summary>
    public int WaitQueueTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum time (seconds) to wait for server selection (finding a suitable replica).
    /// </summary>
    public int ServerSelectionTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum time (seconds) to establish a TCP connection.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Socket timeout (seconds). 0 = no timeout (rely on request timeout middleware).
    /// </summary>
    public int SocketTimeoutSeconds { get; set; } = 0;

    /// <summary>
    /// Gets the final connection string with username/password and pool settings injected.
    /// </summary>
    public string GetConnectionString()
    {
        var baseConnectionString = ConnectionString;

        // Inject username and password if provided
        if (!string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password))
        {
            var uri = new Uri(ConnectionString);
            var protocol = uri.Scheme; // "mongodb" or "mongodb+srv"
            var hostAndPath = ConnectionString.Replace($"{protocol}://", "");
            baseConnectionString = $"{protocol}://{Uri.EscapeDataString(Username)}:{Uri.EscapeDataString(Password)}@{hostAndPath}";
        }

        // Build connection pool parameters
        var poolParams = new List<string>
        {
            $"maxPoolSize={MaxPoolSize}",
            $"minPoolSize={MinPoolSize}",
            $"maxIdleTimeMS={MaxIdleTimeSeconds * 1000}",
            $"waitQueueTimeoutMS={WaitQueueTimeoutSeconds * 1000}",
            $"serverSelectionTimeoutMS={ServerSelectionTimeoutSeconds * 1000}",
            $"connectTimeoutMS={ConnectTimeoutSeconds * 1000}"
        };

        if (SocketTimeoutSeconds > 0)
        {
            poolParams.Add($"socketTimeoutMS={SocketTimeoutSeconds * 1000}");
        }

        var poolQueryString = string.Join("&", poolParams);

        // Append pool parameters to connection string
        if (baseConnectionString.Contains('?'))
        {
            return $"{baseConnectionString}&{poolQueryString}";
        }
        else
        {
            return $"{baseConnectionString}?{poolQueryString}";
        }
    }
}

