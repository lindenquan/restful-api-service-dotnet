using System.Security.Cryptography;
using System.Text;
using Application.Interfaces.Services;

namespace Adapters.Persistence.Security;

/// <summary>
/// Utility for hashing and validating API keys using SHA-256.
/// API keys are hashed before storage - the original key cannot be retrieved.
/// </summary>
public static class ApiKeyHasher
{
    /// <summary>
    /// Generates a new random API key.
    /// </summary>
    /// <param name="length">Length of the key in bytes (default 32 = 256 bits)</param>
    /// <returns>Base64-encoded API key</returns>
    public static string GenerateApiKey(int length = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        // Use URL-safe Base64 encoding
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Hashes an API key using SHA-256.
    /// </summary>
    /// <param name="apiKey">The plain-text API key</param>
    /// <returns>Hex-encoded SHA-256 hash</returns>
    public static string HashApiKey(string apiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Validates an API key against a stored hash.
    /// </summary>
    /// <param name="apiKey">The plain-text API key to validate</param>
    /// <param name="storedHash">The stored SHA-256 hash</param>
    /// <returns>True if the key matches the hash</returns>
    public static bool ValidateApiKey(string apiKey, string storedHash)
    {
        var computedHash = HashApiKey(apiKey);
        // Use constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(storedHash));
    }

    /// <summary>
    /// Gets a prefix of the API key for display/logging purposes.
    /// </summary>
    /// <param name="apiKey">The plain-text API key</param>
    /// <param name="length">Number of characters to show (default 8)</param>
    /// <returns>Prefix like "abc12345..."</returns>
    public static string GetKeyPrefix(string apiKey, int length = 8)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= length)
            return apiKey;

        return $"{apiKey[..length]}...";
    }
}

/// <summary>
/// Injectable implementation of IApiKeyGenerator using ApiKeyHasher.
/// </summary>
public class ApiKeyGeneratorService : IApiKeyGenerator
{
    public string GenerateApiKey() => ApiKeyHasher.GenerateApiKey();
    public string HashApiKey(string apiKey) => ApiKeyHasher.HashApiKey(apiKey);
    public string GetKeyPrefix(string apiKey) => ApiKeyHasher.GetKeyPrefix(apiKey);
}

