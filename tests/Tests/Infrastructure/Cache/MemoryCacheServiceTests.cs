using Adapters.Cache;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Infrastructure.Cache;

/// <summary>
/// Unit tests for MemoryCacheService (L1 in-memory cache).
/// </summary>
public sealed class MemoryCacheServiceTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheSettings _settings;
    private readonly Mock<ILogger<MemoryCacheService>> _loggerMock;
    private readonly MemoryCacheService _sut;

    public MemoryCacheServiceTests()
    {
        var options = new MemoryCacheOptions
        {
            SizeLimit = 100
        };
        _memoryCache = new MemoryCache(options);
        
        _settings = new CacheSettings
        {
            L1 = new L1CacheSettings
            {
                Enabled = true,
                TtlSeconds = 30,
                MaxItems = 100
            }
        };
        
        _loggerMock = new Mock<ILogger<MemoryCacheService>>();
        _sut = new MemoryCacheService(_memoryCache, _settings, _loggerMock.Object);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    [Fact]
    public void Set_ShouldStoreValue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";

        // Act
        _sut.Set(key, value);

        // Assert
        var retrieved = _sut.Get<string>(key);
        retrieved.Should().Be(value);
    }

    [Fact]
    public void Get_NonExistentKey_ShouldReturnDefault()
    {
        // Act
        var result = _sut.Get<string>("non-existent-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGet_ExistingKey_ShouldReturnTrueAndValue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        _sut.Set(key, value);

        // Act
        var exists = _sut.TryGet<string>(key, out var retrieved);

        // Assert
        exists.Should().BeTrue();
        retrieved.Should().Be(value);
    }

    [Fact]
    public void TryGet_NonExistentKey_ShouldReturnFalse()
    {
        // Act
        var exists = _sut.TryGet<string>("non-existent-key", out var retrieved);

        // Assert
        exists.Should().BeFalse();
        retrieved.Should().BeNull();
    }

    [Fact]
    public void Remove_ShouldDeleteValue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        _sut.Set(key, value);

        // Act
        _sut.Remove(key);

        // Assert
        var exists = _sut.TryGet<string>(key, out _);
        exists.Should().BeFalse();
    }

    [Fact]
    public void Exists_ExistingKey_ShouldReturnTrue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        _sut.Set(key, value);

        // Act
        var exists = _sut.Exists(key);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void Exists_NonExistentKey_ShouldReturnFalse()
    {
        // Act
        var exists = _sut.Exists("non-existent-key");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void GetOrAdd_NonExistentKey_ShouldCallFactoryAndCache()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var factoryCalled = false;

        // Act
        var result = _sut.GetOrAdd(key, () =>
        {
            factoryCalled = true;
            return value;
        });

        // Assert
        result.Should().Be(value);
        factoryCalled.Should().BeTrue();
        _sut.Exists(key).Should().BeTrue();
    }

    [Fact]
    public void GetOrAdd_ExistingKey_ShouldNotCallFactory()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        _sut.Set(key, value);
        var factoryCalled = false;

        // Act
        var result = _sut.GetOrAdd(key, () =>
        {
            factoryCalled = true;
            return "different-value";
        });

        // Assert
        result.Should().Be(value);
        factoryCalled.Should().BeFalse();
    }
}

