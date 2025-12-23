using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for logging requests and responses.
/// Logs before handling, after successful handling, and on exceptions.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid().ToString()[..8];  // Short ID for correlation

        // Log before handling
        _logger.LogInformation(
            "[{RequestId}] Handling {RequestName} {@Request}",
            requestId,
            requestName,
            request);

        try
        {
            var response = await next();

            // Log after successful handling
            _logger.LogInformation(
                "[{RequestId}] Handled {RequestName} successfully",
                requestId,
                requestName);

            return response;
        }
        catch (Exception ex)
        {
            // Log on exception
            _logger.LogError(
                ex,
                "[{RequestId}] Error handling {RequestName}: {ErrorMessage}",
                requestId,
                requestName,
                ex.Message);

            throw;
        }
    }
}

