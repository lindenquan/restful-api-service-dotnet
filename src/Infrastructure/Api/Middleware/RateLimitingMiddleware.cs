using System.Text.Json;

namespace Infrastructure.Api.Middleware;

/// <summary>
/// Middleware that monitors system resources (memory, CPU, thread pool) and
/// returns 429 Too Many Requests when the system is under pressure.
/// This prevents OutOfMemoryException by rate limiting before it occurs.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingSettings _settings;

    // Cache GC info check - don't check every request
    private DateTime _lastCheck = DateTime.MinValue;
    private bool _isUnderPressure;
    private string _pressureReason = string.Empty;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitingSettings settings)
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

        // Check pressure at configured interval (not every request)
        if (ShouldCheckPressure())
        {
            CheckSystemPressure();
        }

        if (_isUnderPressure)
        {
            await RejectRequest(context);
            return;
        }

        await _next(context);
    }

    private bool ShouldCheckPressure()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastCheck).TotalMilliseconds < _settings.CheckIntervalMs)
        {
            return false;
        }
        _lastCheck = now;
        return true;
    }

    private void CheckSystemPressure()
    {
        var reasons = new List<string>();

        // Check memory pressure using GC info
        var gcInfo = GC.GetGCMemoryInfo();
        var memoryLoadPercent = gcInfo.TotalAvailableMemoryBytes > 0
            ? (double)gcInfo.MemoryLoadBytes / gcInfo.TotalAvailableMemoryBytes * 100
            : 0;

        if (memoryLoadPercent >= _settings.MemoryThresholdPercent)
        {
            reasons.Add($"Memory: {memoryLoadPercent:F1}% >= {_settings.MemoryThresholdPercent}%");
        }

        // Check if we're in high memory load condition (GC's own threshold)
        if (gcInfo.MemoryLoadBytes >= gcInfo.HighMemoryLoadThresholdBytes)
        {
            reasons.Add($"GC HighMemoryLoad triggered");
        }

        // Check thread pool exhaustion
        ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
        ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);

        var workerUtilization = (double)(maxWorkers - availableWorkers) / maxWorkers * 100;
        var ioUtilization = (double)(maxIo - availableIo) / maxIo * 100;

        if (workerUtilization >= _settings.ThreadPoolThresholdPercent)
        {
            reasons.Add($"ThreadPool Workers: {workerUtilization:F1}% >= {_settings.ThreadPoolThresholdPercent}%");
        }

        if (ioUtilization >= _settings.ThreadPoolThresholdPercent)
        {
            reasons.Add($"ThreadPool IO: {ioUtilization:F1}% >= {_settings.ThreadPoolThresholdPercent}%");
        }

        // Check pending work items (queue depth)
        var pendingWork = ThreadPool.PendingWorkItemCount;
        if (pendingWork >= _settings.PendingWorkItemsThreshold)
        {
            reasons.Add($"PendingWorkItems: {pendingWork} >= {_settings.PendingWorkItemsThreshold}");
        }

        var wasUnderPressure = _isUnderPressure;
        _isUnderPressure = reasons.Count > 0;
        _pressureReason = string.Join("; ", reasons);

        // Log state changes
        if (_isUnderPressure && !wasUnderPressure)
        {
            _logger.LogWarning(
                "Rate limiting ACTIVATED: {Reason}. Returning 429 for new requests.",
                _pressureReason);
        }
        else if (!_isUnderPressure && wasUnderPressure)
        {
            _logger.LogInformation("Rate limiting DEACTIVATED. Accepting requests normally.");
        }
    }

    private async Task RejectRequest(HttpContext context)
    {
        _logger.LogWarning(
            "Request rejected due to system pressure: {Reason}. Path: {Path}",
            _pressureReason,
            context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers["Retry-After"] = _settings.RetryAfterSeconds.ToString();

        var response = new
        {
            type = "https://httpstatuses.com/429",
            title = "Too Many Requests",
            status = 429,
            message = "Server is under heavy load. Please retry later.",
            reason = _pressureReason
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}

