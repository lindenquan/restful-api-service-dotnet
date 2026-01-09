using Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using StackExchange.Redis;

namespace Tests.Infrastructure.Cache;

/// <summary>
/// Unit tests for RemoteCacheService (Redis cache with lock-based consistency).
/// </summary>
public sealed class RemoteCacheServiceTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly CacheSettings _settings;
    private readonly Mock<ILogger<RemoteCacheService>> _loggerMock;
    private readonly RemoteCacheService _sut;

    public RemoteCacheServiceTests()
    {
        _databaseMock = new Mock<IDatabase>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _settings = new CacheSettings
        {
            Remote = new RemoteCacheSettings
            {
                Enabled = true,
                ConnectionString = "localhost:6379",
                InstanceName = "Test:",
                TtlSeconds = 60,
                LockTimeoutSeconds = 30,
                LockWaitTimeoutMs = 1000,
                LockRetryDelayMs = 50
            }
        };

        _loggerMock = new Mock<ILogger<RemoteCacheService>>();
        _sut = new RemoteCacheService(_redisMock.Object, _settings, _loggerMock.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    #region Set Tests

    [Fact]
    public void Set_ShouldNotThrow()
    {
        // Arrange
        var key = "test-key";
        var value = new TestData { Id = 1, Name = "Test" };

        // Act & Assert - should not throw even if the mock doesn't return anything
        var exception = Record.Exception(() => _sut.Set(key, value));
        exception.ShouldBeNull();
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_ExistingKey_ShouldReturnTrueAndValue()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = new TestData { Id = 1, Name = "Test" };
        var json = System.Text.Json.JsonSerializer.Serialize(expectedValue,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(new RedisValue(json));

        // Act
        var exists = _sut.TryGet<TestData>(key, out var retrieved);

        // Assert
        exists.ShouldBeTrue();
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe(expectedValue.Id);
        retrieved.Name.ShouldBe(expectedValue.Name);
    }

    [Fact]
    public void TryGet_NonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(RedisValue.Null);

        // Act
        var exists = _sut.TryGet<string>("non-existent-key", out var retrieved);

        // Assert
        exists.ShouldBeFalse();
        retrieved.ShouldBeNull();
    }

    #endregion

    #region Lock Tests

    [Fact]
    public void AcquireLock_ShouldReturnNullWhenRedisReturnsFailure()
    {
        // Arrange - When Redis returns false (lock already held), AcquireLock returns null
        // The mock default returns false, so AcquireLock should return null
        var key = "test-key";

        // Act
        var token = _sut.AcquireLock(key);

        // Assert - default mock behavior returns false, so token should be null
        token.ShouldBeNull();
    }

    [Fact]
    public void IsLocked_ShouldReturnTrue_WhenLockExists()
    {
        // Arrange
        var key = "test-key";
        _databaseMock.Setup(d => d.KeyExists(
                It.Is<RedisKey>(k => k.ToString().Contains("lock:")),
                It.IsAny<CommandFlags>()))
            .Returns(true);

        // Act
        var isLocked = _sut.IsLocked(key);

        // Assert
        isLocked.ShouldBeTrue();
    }

    [Fact]
    public void IsLocked_ShouldReturnFalse_WhenNoLockExists()
    {
        // Arrange
        var key = "test-key";
        _databaseMock.Setup(d => d.KeyExists(
                It.Is<RedisKey>(k => k.ToString().Contains("lock:")),
                It.IsAny<CommandFlags>()))
            .Returns(false);

        // Act
        var isLocked = _sut.IsLocked(key);

        // Assert
        isLocked.ShouldBeFalse();
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_ShouldDeleteKey()
    {
        // Arrange
        var key = "test-key";

        // Act
        _sut.Remove(key);

        // Assert
        _databaseMock.Verify(
            d => d.KeyDelete(
                It.Is<RedisKey>(k => k.ToString().Contains(key)),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    #endregion

    #region Exists Tests

    [Fact]
    public void Exists_ExistingKey_ShouldReturnTrue()
    {
        // Arrange
        var key = "test-key";
        _databaseMock.Setup(d => d.KeyExists(
                It.Is<RedisKey>(k => !k.ToString().Contains("lock:") && k.ToString().Contains(key)),
                It.IsAny<CommandFlags>()))
            .Returns(true);

        // Act
        var exists = _sut.Exists(key);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public void Exists_NonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        _databaseMock.Setup(d => d.KeyExists(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(false);

        // Act
        var exists = _sut.Exists("non-existent-key");

        // Assert
        exists.ShouldBeFalse();
    }

    #endregion

    #region CacheReadResult Tests

    [Fact]
    public void CacheReadResult_Hit_ShouldHaveCorrectProperties()
    {
        // Act
        var result = CacheReadResult<string>.Hit("test-value");

        // Assert
        result.Found.ShouldBeTrue();
        result.Value.ShouldBe("test-value");
        result.IsLocked.ShouldBeFalse();
    }

    [Fact]
    public void CacheReadResult_Miss_ShouldHaveCorrectProperties()
    {
        // Act
        var result = CacheReadResult<string>.Miss();

        // Assert
        result.Found.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.IsLocked.ShouldBeFalse();
    }

    [Fact]
    public void CacheReadResult_Locked_ShouldHaveCorrectProperties()
    {
        // Act
        var result = CacheReadResult<string>.Locked();

        // Assert
        result.Found.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.IsLocked.ShouldBeTrue();
    }

    #endregion

    private class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}

