namespace Adapters.Api.Middleware;

/// <summary>
/// Middleware to add security headers to all responses.
/// Protects against common web vulnerabilities.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var isSwaggerPath = path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);

        // Prevent MIME type sniffing
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking by disallowing framing
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // Enable XSS filter in older browsers
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Control referrer information
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Prevent caching of sensitive data (can be overridden per endpoint)
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";

        // Content Security Policy
        if (isSwaggerPath)
        {
            // Relaxed CSP for Swagger UI (only enabled in dev environment)
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;";
        }
        else
        {
            // Restrictive CSP for API endpoints
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        }

        // Permissions Policy - disable unnecessary browser features
        context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

        await _next(context);
    }
}

/// <summary>
/// Extension method for adding security headers middleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}

