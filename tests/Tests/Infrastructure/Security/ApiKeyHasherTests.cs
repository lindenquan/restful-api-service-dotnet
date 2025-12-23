using Adapters.Persistence.Security;
using FluentAssertions;

namespace Tests.Infrastructure.Security;

/// <summary>
/// Unit tests for ApiKeyHasher utility.
/// </summary>
public class ApiKeyHasherTests
{
    [Fact]
    public void GenerateApiKey_ShouldReturnNonEmptyString()
    {
        // Act
        var apiKey = ApiKeyHasher.GenerateApiKey();

        // Assert
        apiKey.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateApiKey_ShouldReturnUrlSafeBase64()
    {
        // Act
        var apiKey = ApiKeyHasher.GenerateApiKey();

        // Assert - URL-safe Base64 should not contain +, /, or =
        apiKey.Should().NotContain("+");
        apiKey.Should().NotContain("/");
        apiKey.Should().NotContain("=");
    }

    [Fact]
    public void GenerateApiKey_ShouldGenerateUniqueKeys()
    {
        // Act
        var key1 = ApiKeyHasher.GenerateApiKey();
        var key2 = ApiKeyHasher.GenerateApiKey();

        // Assert
        key1.Should().NotBe(key2);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void GenerateApiKey_WithDifferentLengths_ShouldReturnDifferentLengthKeys(int length)
    {
        // Act
        var apiKey = ApiKeyHasher.GenerateApiKey(length);

        // Assert - Base64 encoded string length should be roughly 4/3 of byte length
        apiKey.Should().NotBeNullOrWhiteSpace();
        apiKey.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void HashApiKey_ShouldReturnConsistentHash()
    {
        // Arrange
        var apiKey = "test-api-key-123";

        // Act
        var hash1 = ApiKeyHasher.HashApiKey(apiKey);
        var hash2 = ApiKeyHasher.HashApiKey(apiKey);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashApiKey_ShouldReturn64CharHexString()
    {
        // Arrange
        var apiKey = "test-api-key-123";

        // Act
        var hash = ApiKeyHasher.HashApiKey(apiKey);

        // Assert - SHA-256 produces 256 bits = 64 hex characters
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[a-f0-9]+$"); // lowercase hex
    }

    [Fact]
    public void HashApiKey_ShouldReturnLowercaseHex()
    {
        // Arrange
        var apiKey = "TEST-API-KEY";

        // Act
        var hash = ApiKeyHasher.HashApiKey(apiKey);

        // Assert
        hash.Should().Be(hash.ToLowerInvariant());
    }

    [Fact]
    public void HashApiKey_DifferentKeys_ShouldReturnDifferentHashes()
    {
        // Act
        var hash1 = ApiKeyHasher.HashApiKey("key1");
        var hash2 = ApiKeyHasher.HashApiKey("key2");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ValidateApiKey_WithCorrectKey_ShouldReturnTrue()
    {
        // Arrange
        var apiKey = "my-secret-api-key";
        var hash = ApiKeyHasher.HashApiKey(apiKey);

        // Act
        var isValid = ApiKeyHasher.ValidateApiKey(apiKey, hash);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateApiKey_WithIncorrectKey_ShouldReturnFalse()
    {
        // Arrange
        var apiKey = "my-secret-api-key";
        var hash = ApiKeyHasher.HashApiKey(apiKey);

        // Act
        var isValid = ApiKeyHasher.ValidateApiKey("wrong-key", hash);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateApiKey_CaseSensitive_ShouldReturnFalse()
    {
        // Arrange
        var apiKey = "MySecretKey";
        var hash = ApiKeyHasher.HashApiKey(apiKey);

        // Act
        var isValid = ApiKeyHasher.ValidateApiKey("mysecretkey", hash);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("abcdefghijklmnop", 8, "abcdefgh...")]
    [InlineData("12345678", 8, "12345678")]
    [InlineData("short", 8, "short")]
    [InlineData("", 8, "")]
    public void GetKeyPrefix_ShouldReturnCorrectPrefix(string apiKey, int length, string expected)
    {
        // Act
        var prefix = ApiKeyHasher.GetKeyPrefix(apiKey, length);

        // Assert
        prefix.Should().Be(expected);
    }
}

