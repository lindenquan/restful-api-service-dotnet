namespace Infrastructure.Resilience;

/// <summary>
/// Helper class to determine if exceptions are transient and should trigger retries.
/// </summary>
public static class TransientExceptionHelper
{
    /// <summary>
    /// Determine if an exception is transient and should be retried.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>True if the exception is transient and the operation should be retried.</returns>
    public static bool IsTransient(Exception ex)
    {
        // Don't retry cancellations (TaskCanceledException inherits from OperationCanceledException)
        if (ex is OperationCanceledException)
            return false;

        return ex switch
        {
            // MongoDB transient errors
            MongoDB.Driver.MongoConnectionException => true,
            MongoDB.Driver.MongoExecutionTimeoutException => true,
            MongoDB.Driver.MongoCursorNotFoundException => true,

            // Redis transient errors
            StackExchange.Redis.RedisConnectionException => true,
            StackExchange.Redis.RedisTimeoutException => true,
            StackExchange.Redis.RedisServerException rse when rse.Message.Contains("BUSY") => true,

            // Network errors
            System.Net.Sockets.SocketException => true,
            System.IO.IOException => true,
            TimeoutException => true,

            // Default: don't retry unknown exceptions
            _ => false
        };
    }
}

