namespace Application.Interfaces;

/// <summary>
/// Marker interface for MediatR requests that should be retried on transient failures.
/// Requests implementing this interface will automatically be wrapped with retry logic.
/// </summary>
public interface IRetryableRequest
{
    /// <summary>
    /// Maximum number of retry attempts. If null, uses default from configuration.
    /// </summary>
    int? MaxRetryAttempts => null;
}

