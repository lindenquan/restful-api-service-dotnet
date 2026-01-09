using Infrastructure.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Tests.Infrastructure.Cache;

/// <summary>
/// Unit tests for LocalCacheService (in-memory cache for static data).
/// </summary>
public sealed class LocalCacheServiceTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheSettings _settings;
    private readonly Mock<ILogger<LocalCacheService>> _loggerMock;
    private readonly LocalCacheService _sut;

    public LocalCacheServiceTests()
    {
        var options = new MemoryCacheOptions
        {
            SizeLimit = 100
        };
        _memoryCache = new MemoryCache(options);

        _settings = new CacheSettings
        {
            Local = new LocalCacheSettings
            {
                Enabled = true,
                MaxItems = 100
            }
        };

        _loggerMock = new Mock<ILogger<LocalCacheService>>();
        _sut = new LocalCacheService(_memoryCache, _settings, _loggerMock.Object);
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
        retrieved.ShouldBe(value);
    }

    [Fact]
    public void Get_NonExistentKey_ShouldReturnDefault()
    {
        // Act
        var result = _sut.Get<string>("non-existent-key");

        // Assert
        result.ShouldBeNull();
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
        exists.ShouldBeTrue();
        retrieved.ShouldBe(value);
    }

    [Fact]
    public void TryGet_NonExistentKey_ShouldReturnFalse()
    {
        // Act
        var exists = _sut.TryGet<string>("non-existent-key", out var retrieved);

        // Assert
        exists.ShouldBeFalse();
        retrieved.ShouldBeNull();
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
        exists.ShouldBeFalse();
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
        exists.ShouldBeTrue();
    }

    [Fact]
    public void Exists_NonExistentKey_ShouldReturnFalse()
    {
        // Act
        var exists = _sut.Exists("non-existent-key");

        // Assert
        exists.ShouldBeFalse();
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
        result.ShouldBe(value);
        factoryCalled.ShouldBeTrue();
        _sut.Exists(key).ShouldBeTrue();
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
        result.ShouldBe(value);
        factoryCalled.ShouldBeFalse();
    }
}

