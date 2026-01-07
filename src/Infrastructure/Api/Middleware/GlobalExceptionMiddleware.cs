using System.Net;
using System.Text.Json;

namespace Infrastructure.Api.Middleware;

/// <summary>
/// Global exception handler middleware.
/// Catches all unhandled exceptions and returns appropriate error responses.
/// In production, stack traces and internal details are hidden.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorType, message) = exception switch
        {
            OutOfMemoryException => (HttpStatusCode.ServiceUnavailable, "ServiceUnavailable", "The service is temporarily unavailable due to resource constraints. Please retry later."),
            ArgumentNullException => (HttpStatusCode.BadRequest, "BadRequest", "A required parameter was missing."),
            ArgumentException => (HttpStatusCode.BadRequest, "BadRequest", exception.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, "NotFound", "The requested resource was not found."),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Forbidden", "You do not have permission to access this resource."),
            OperationCanceledException => (HttpStatusCode.BadRequest, "RequestCancelled", "The request was cancelled."),
            TimeoutException => (HttpStatusCode.GatewayTimeout, "Timeout", "The operation timed out."),
            _ => (HttpStatusCode.InternalServerError, "InternalServerError", "An unexpected error occurred.")
        };

        // For OOM, add Retry-After header to help clients back off
        if (exception is OutOfMemoryException)
        {
            context.Response.Headers["Retry-After"] = "30";
        }

        // Log the full exception
        _logger.LogError(
            exception,
            "Unhandled exception: {ExceptionType} - {Message}. TraceId: {TraceId}",
            exception.GetType().Name,
            exception.Message,
            context.TraceIdentifier);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Title = errorType,
            Status = (int)statusCode,
            TraceId = context.TraceIdentifier,
            Message = message
        };

        // Include details only in development
        if (_environment.IsDevelopment())
        {
            response.Detail = exception.Message;
            response.StackTrace = exception.StackTrace;
        }

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Standard error response format.
/// </summary>
public class ErrorResponse
{
    public string Type { get; set; } = default!;
    public string Title { get; set; } = default!;
    public int Status { get; set; }
    public string TraceId { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? Detail { get; set; }
    public string? StackTrace { get; set; }
}

/// <summary>
/// Extension method for adding global exception handler middleware.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}

