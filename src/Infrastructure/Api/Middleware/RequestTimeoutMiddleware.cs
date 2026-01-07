using System.Text.Json;

namespace Infrastructure.Api.Middleware;

/// <summary>
/// Middleware that enforces request timeouts at two levels:
///
/// 1. Processing Timeout (DefaultTimeoutSeconds):
///    - Covers controller execution, DB queries, serialization
///    - Triggers OperationCanceledException
///    - Returns 408 if response hasn't started
///
/// 2. Total Timeout (TotalTimeoutSeconds):
///    - Covers ENTIRE request including client downloading response
///    - Hard limit - aborts connection via context.Abort()
///    - Ensures slow clients can't hold connections indefinitely
///    - Critical for graceful shutdown to complete on time
///
/// Why this is needed:
/// - Kestrel has NO built-in request processing timeout (unlike Tomcat, Nginx, IIS)
/// - MinResponseDataRate protects against extremely slow clients but has no max time
/// - Without total timeout, a slow client could delay graceful shutdown
/// </summary>
public sealed class RequestTimeoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimeoutMiddleware> _logger;
    private readonly RequestTimeoutSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RequestTimeoutMiddleware(
        RequestDelegate next,
        ILogger<RequestTimeoutMiddleware> logger,
        RequestTimeoutSettings settings)
    {
        _next = next;
        _logger = logger;
        _settings = settings;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_settings.Enabled)
        {
            await _next(context);
            return;
        }

        var processingTimeout = GetProcessingTimeoutForPath(context.Request.Path);
        var originalToken = context.RequestAborted;

        // === Processing Timeout ===
        // Cancels RequestAborted token, triggers OperationCanceledException
        using var processingCts = CancellationTokenSource.CreateLinkedTokenSource(originalToken);
        processingCts.CancelAfter(processingTimeout);
        context.RequestAborted = processingCts.Token;

        // === Total Timeout ===
        // Hard limit - aborts connection regardless of response state
        // This ensures slow client downloads can't hold connections forever
        CancellationTokenSource? totalCts = null;
        CancellationTokenRegistration? totalTimeoutRegistration = null;

        if (_settings.TotalTimeoutSeconds > 0)
        {
            totalCts = new CancellationTokenSource();
            totalCts.CancelAfter(TimeSpan.FromSeconds(_settings.TotalTimeoutSeconds));

            totalTimeoutRegistration = totalCts.Token.Register(
                static state =>
                {
                    var (ctx, logger, totalSeconds) = ((HttpContext, ILogger, int))state!;
                    logger.LogWarning(
                        "Total request timeout ({TotalTimeout}s) exceeded. Aborting connection. " +
                        "Path: {Path}, Method: {Method}",
                        totalSeconds,
                        ctx.Request.Path,
                        ctx.Request.Method);
                    ctx.Abort();
                },
                (context, _logger, _settings.TotalTimeoutSeconds),
                useSynchronizationContext: false);
        }

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (processingCts.IsCancellationRequested && !originalToken.IsCancellationRequested)
        {
            // Processing timeout occurred (not client disconnect)
            _logger.LogWarning(
                "Request processing timeout after {Timeout}s. Path: {Path}, Method: {Method}",
                processingTimeout.TotalSeconds,
                context.Request.Path,
                context.Request.Method);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    type = "https://httpstatuses.com/408",
                    title = "Request Timeout",
                    status = 408,
                    message = $"Request processing exceeded the timeout of {processingTimeout.TotalSeconds} seconds.",
                    traceId = context.TraceIdentifier
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
            }
        }
        finally
        {
            // Dispose total timeout to prevent callback from firing after request completes
            if (totalTimeoutRegistration.HasValue)
            {
                await totalTimeoutRegistration.Value.DisposeAsync();
            }
            totalCts?.Dispose();
        }
    }

    private TimeSpan GetProcessingTimeoutForPath(PathString path)
    {
        // Check for endpoint-specific timeout
        foreach (var (pathPrefix, timeoutSeconds) in _settings.EndpointTimeouts)
        {
            if (path.StartsWithSegments(pathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return TimeSpan.FromSeconds(timeoutSeconds);
            }
        }

        return TimeSpan.FromSeconds(_settings.DefaultTimeoutSeconds);
    }
}

/// <summary>
/// Extension methods for request timeout middleware.
/// </summary>
public static class RequestTimeoutExtensions
{
    /// <summary>
    /// Adds request timeout middleware to the pipeline.
    /// Should be added early in the pipeline (after exception handling, before routing).
    /// </summary>
    public static IApplicationBuilder UseRequestTimeout(this IApplicationBuilder builder)
        => builder.UseMiddleware<RequestTimeoutMiddleware>();
}

