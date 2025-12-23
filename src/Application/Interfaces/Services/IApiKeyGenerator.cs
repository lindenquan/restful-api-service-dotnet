namespace Application.Interfaces.Services;

/// <summary>
/// Interface for API key generation (allows testing/mocking).
/// </summary>
public interface IApiKeyGenerator
{
    /// <summary>
    /// Generates a new random API key.
    /// </summary>
    string GenerateApiKey();

    /// <summary>
    /// Hashes an API key using SHA-256.
    /// </summary>
    string HashApiKey(string apiKey);

    /// <summary>
    /// Gets a prefix of the API key for display/logging purposes.
    /// </summary>
    string GetKeyPrefix(string apiKey);
}

