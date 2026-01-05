using System.Net.Sockets;
using Infrastructure.Resilience;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Tests.Infrastructure.Resilience;

/// <summary>
/// Tests for transient exception detection in resilience policies.
/// </summary>
public class TransientExceptionDetectionTests
{
    [Theory]
    [InlineData(typeof(MongoConnectionException))]
    [InlineData(typeof(SocketException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(TimeoutException))]
    public void IsTransientException_MongoAndNetworkExceptions_ReturnsTrue(Type exceptionType)
    {
        // Arrange
        Exception exception = exceptionType.Name switch
        {
            nameof(MongoConnectionException) => CreateMongoConnectionException(),
            nameof(SocketException) => new SocketException(),
            nameof(IOException) => new IOException("Network error"),
            nameof(TimeoutException) => new TimeoutException(),
            _ => throw new ArgumentException($"Unknown exception type: {exceptionType}")
        };

        // Act
        var result = TransientExceptionHelper.IsTransient(exception);

        // Assert
        Assert.True(result, $"{exceptionType.Name} should be considered transient");
    }

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(NullReferenceException))]
    [InlineData(typeof(NotSupportedException))]
    public void IsTransientException_NonTransientExceptions_ReturnsFalse(Type exceptionType)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test message")!;

        // Act
        var result = TransientExceptionHelper.IsTransient(exception);

        // Assert
        Assert.False(result, $"{exceptionType.Name} should NOT be considered transient");
    }

    [Fact]
    public void IsTransientException_OperationCanceledException_ReturnsFalse()
    {
        // Arrange
        var exception = new OperationCanceledException();

        // Act
        var result = TransientExceptionHelper.IsTransient(exception);

        // Assert
        Assert.False(result, "OperationCanceledException should NOT be retried");
    }

    [Fact]
    public void IsTransientException_TaskCanceledException_ReturnsFalse()
    {
        // Arrange
        var exception = new TaskCanceledException();

        // Act
        var result = TransientExceptionHelper.IsTransient(exception);

        // Assert
        Assert.False(result, "TaskCanceledException should NOT be retried");
    }

    [Fact]
    public void IsTransientException_RedisConnectionException_ReturnsTrue()
    {
        // Arrange
        var exception = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed");

        // Act
        var result = TransientExceptionHelper.IsTransient(exception);

        // Assert
        Assert.True(result, "RedisConnectionException should be considered transient");
    }

    [Fact]
    public void IsTransientException_RedisServerExceptionWithBusy_ReturnsTrue()
    {
        // Arrange
        var exception = new RedisServerException("BUSY Redis is busy running a script");

        // Act
        var result = TransientExceptionHelper.IsTransient(exception);

        // Assert
        Assert.True(result, "RedisServerException with BUSY should be considered transient");
    }

    [Fact]
    public void IsTransientException_RedisServerExceptionWithoutBusy_ReturnsFalse()
    {
        // Arrange
        var exception = new RedisServerException("ERR unknown command");

        // Act
        var result = TransientExceptionHelper.IsTransient(exception);

        // Assert
        Assert.False(result, "RedisServerException without BUSY should NOT be considered transient");
    }

    private static MongoConnectionException CreateMongoConnectionException()
    {
        var endPoint = new System.Net.DnsEndPoint("localhost", 27017);
        var serverId = new MongoDB.Driver.Core.Servers.ServerId(
            new MongoDB.Driver.Core.Clusters.ClusterId(1), endPoint);
        var connectionId = new MongoDB.Driver.Core.Connections.ConnectionId(serverId);
        return new MongoConnectionException(connectionId, "Connection failed");
    }
}

