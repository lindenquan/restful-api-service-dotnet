using Application;
using Asp.Versioning;
using DotNetEnv;
using DTOs.Shared;
using Infrastructure.Api.Authentication;
using Infrastructure.Api.Authorization;
using Infrastructure.Api.Configuration;
using Infrastructure.Api.Middleware;
using Infrastructure.Api.Services;
using Infrastructure.Cache;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Configuration;
using Infrastructure.Resilience;
using Microsoft.OpenApi;

// Load .env file from solution root (must be done before WebApplication.CreateBuilder)
// This allows setting ASPNETCORE_ENVIRONMENT and other variables in .env file
var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
if (solutionRoot != null)
{
    var envFile = Path.Combine(solutionRoot, ".env");
    if (File.Exists(envFile))
    {
        Env.Load(envFile);
        Console.WriteLine($"Loaded environment variables from: {envFile}");
    }
}

var builder = WebApplication.CreateBuilder(args);

// Require explicit environment specification - fail fast if not set
// Use: Set ASPNETCORE_ENVIRONMENT in .env file, or use command line:
//      dotnet run --project src/Adapters --environment local
var env = builder.Environment.EnvironmentName;
var validEnvironments = new[] { "local", "dev", "stage", "prod", "amr-prod", "eu-prod", "amr-stage", "eu-stage" };

if (string.IsNullOrEmpty(env) ||
    env.Equals("Production", StringComparison.OrdinalIgnoreCase) ||
    env.Equals("Development", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("ERROR: Environment must be explicitly specified.");
    Console.Error.WriteLine($"Valid environments: {string.Join(", ", validEnvironments)}");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  1. Set ASPNETCORE_ENVIRONMENT in .env file (recommended)");
    Console.Error.WriteLine("  2. dotnet run --project src/Adapters --environment local");
    Console.Error.WriteLine("  3. $env:ASPNETCORE_ENVIRONMENT=\"local\"; dotnet run --project src/Adapters");
    Environment.Exit(1);
}

// Load configuration from solution-level config folder
// In Docker: /app/config, in development: {solutionRoot}/config
var configPath = Path.Combine(builder.Environment.ContentRootPath, "config");
if (!Directory.Exists(configPath))
{
    // Development: navigate up from src/Adapters to solution root
    configPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "config");
}

builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(configPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);

// Environment chaining for regional deployments
// amr-prod and eu-prod extend prod; amr-stage and eu-stage extend stage
if (env.EndsWith("-prod", StringComparison.OrdinalIgnoreCase))
{
    builder.Configuration.AddJsonFile("appsettings.prod.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);
}
else if (env.EndsWith("-stage", StringComparison.OrdinalIgnoreCase))
{
    builder.Configuration.AddJsonFile("appsettings.stage.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);
}

builder.Configuration.AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings (not numbers) for better API readability
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Prescription Order API",
        Version = "v1",
        Description = "RESTful API for managing prescription orders"
    });
    options.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "Prescription Order API",
        Version = "v2",
        Description = "RESTful API for managing prescription orders (v2)"
    });

    // Filter endpoints by API version using ApiVersion metadata
    options.DocInclusionPredicate((docName, apiDesc) =>
    {
        // Get API version from the endpoint metadata
        var actionApiVersionModel = apiDesc.ActionDescriptor.EndpointMetadata
            .OfType<ApiVersionAttribute>()
            .FirstOrDefault();

        if (actionApiVersionModel != null)
        {
            var versions = actionApiVersionModel.Versions;
            if (docName == "v1" && versions.Any(v => v.MajorVersion == 1))
                return true;
            if (docName == "v2" && versions.Any(v => v.MajorVersion == 2))
                return true;
            return false;
        }

        // Include non-versioned endpoints in v1 only
        return docName == "v1";
    });

    // Add API Key authentication to Swagger UI
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API Key authentication"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
    });
});

// Configure API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc();

// Register Application layer services (includes MediatR, validators, behaviors)
builder.Services.AddApplication();

// Load configuration from appsettings
var mongoDbSettings = builder.Configuration
    .GetSection(MongoDbSettings.SectionName)
    .Get<MongoDbSettings>() ?? throw new InvalidOperationException("MongoDbSettings is required");

var cacheSettings = builder.Configuration
    .GetSection(CacheSettings.SectionName)
    .Get<CacheSettings>() ?? new CacheSettings();

var corsSettings = builder.Configuration
    .GetSection(CorsSettings.SectionName)
    .Get<CorsSettings>() ?? new CorsSettings();

var rateLimitingSettings = builder.Configuration
    .GetSection(RateLimitingSettings.SectionName)
    .Get<RateLimitingSettings>() ?? new RateLimitingSettings();

var requestTimeoutSettings = builder.Configuration
    .GetSection("RequestTimeout")
    .Get<RequestTimeoutSettings>() ?? new RequestTimeoutSettings();

var paginationSettings = builder.Configuration
    .GetSection(PaginationSettings.SectionName)
    .Get<PaginationSettings>() ?? new PaginationSettings();

var swaggerEnabled = builder.Configuration.GetValue<bool?>("Swagger:Enabled") ?? false;

// Register Persistence layer services (MongoDB)
builder.Services.AddPersistence(mongoDbSettings);

// Register Cache layer services (L1 memory / L2 Redis)
builder.Services.AddCache(cacheSettings);

// Register Resilience pipelines (retry, circuit breaker, timeout)
builder.Services.AddResilience(builder.Configuration);

// Configure Root Admin settings and auto-creation
builder.Services.Configure<RootAdminSettings>(
    builder.Configuration.GetSection(RootAdminSettings.SectionName));
builder.Services.AddHostedService<RootAdminInitializer>();
builder.Services.AddHostedService<SystemMetricsService>();

// Register Rate Limiting, Request Timeout, and Pagination settings
builder.Services.AddSingleton(rateLimitingSettings);
builder.Services.AddSingleton(requestTimeoutSettings);
builder.Services.AddSingleton(paginationSettings);

// Configure Graceful Shutdown
// This allows in-flight requests to complete before the application shuts down
var gracefulShutdownSettings = builder.Configuration
    .GetSection(GracefulShutdownSettings.SectionName)
    .Get<GracefulShutdownSettings>() ?? new GracefulShutdownSettings();

builder.Host.ConfigureHostOptions(options =>
{
    // ShutdownTimeout controls how long the host waits for graceful shutdown
    // after IHostApplicationLifetime.StopApplication() is called.
    // This should be LESS than Kubernetes terminationGracePeriodSeconds (60s)
    options.ShutdownTimeout = TimeSpan.FromSeconds(gracefulShutdownSettings.ShutdownTimeoutSeconds);
});

// Configure API Key Authentication
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddAuthentication(ApiKeyAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.AuthenticationScheme,
        options => { });

// Configure Authorization Policies
builder.Services.AddApiKeyAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        // If no origins specified, allow any (development mode)
        if (corsSettings.AllowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(corsSettings.AllowedOrigins);
            if (corsSettings.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        }

        policy.WithMethods(corsSettings.AllowedMethods)
              .WithHeaders(corsSettings.AllowedHeaders)
              .WithExposedHeaders(corsSettings.ExposedHeaders)
              .SetPreflightMaxAge(TimeSpan.FromSeconds(corsSettings.PreflightMaxAgeSeconds));
    });
});

// Configure Health Checks
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

// Add MongoDB health check - required for application to be healthy
// Use GetConnectionString() to include username/password if configured
if (!string.IsNullOrEmpty(mongoDbSettings?.ConnectionString))
{
    var mongoConnectionString = mongoDbSettings.GetConnectionString();
    healthChecksBuilder.AddMongoDb(
        sp => new MongoDB.Driver.MongoClient(mongoConnectionString),
        name: "mongodb",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: ["db", "mongodb", "required"]);
}

// Add Redis health check if L2 cache is enabled
// - Startup: Redis must be healthy (app fails to start if unavailable)
// - Runtime: Reports Degraded (not Unhealthy) when Redis is down
// - App continues to work without L2 cache after startup, logging errors
if (cacheSettings.L2.Enabled)
{
    healthChecksBuilder.AddRedis(
        cacheSettings.L2.ConnectionString,
        name: "redis",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: ["cache", "redis"]);
}

// Add Root Admin health check - required for application to be healthy
// Verifies that a root admin user exists in the database
healthChecksBuilder.AddCheck<RootAdminHealthCheck>(
    "root-admin",
    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
    tags: ["security", "required"]);

// Configure Antiforgery for CSRF protection (for browser clients with cookies)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // Allow JavaScript to read for SPA
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// Configure the HTTP request pipeline

// 1. Global exception handler (first to catch all errors)
app.UseGlobalExceptionHandler();

// 2. Request timeout (prevent runaway requests - Kestrel has no default!)
if (requestTimeoutSettings.Enabled)
{
    app.UseRequestTimeout();
}

// 3. Rate limiting (reject requests when system is under pressure)
if (rateLimitingSettings.Enabled)
{
    app.UseRateLimiting();
}

// 4. Security headers
app.UseSecurityHeaders();

// 5. HTTPS redirection
app.UseHttpsRedirection();

// 6. CORS (must be before auth and routing)
app.UseCors("DefaultPolicy");

// 7. Validation exception handler
app.UseValidationExceptionHandler();

// 8. Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// 9. Health check endpoint - returns Unhealthy if MongoDB or Redis (when L2 enabled) are down
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

// 10. OpenAPI & Swagger UI - controlled by configuration (Swagger:Enabled)
if (swaggerEnabled)
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Prescription Order API v1");
        options.SwaggerEndpoint("/swagger/v2/swagger.json", "Prescription Order API v2");
        options.RoutePrefix = "swagger";
    });
}

// 11. Controllers
app.MapControllers();

// 12. Register graceful shutdown logging
// IHostApplicationLifetime provides hooks for application lifecycle events
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var shutdownLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Shutdown");

lifetime.ApplicationStarted.Register(() =>
    shutdownLogger.LogInformation(
        "Application started. Graceful shutdown timeout: {Timeout}s",
        gracefulShutdownSettings.ShutdownTimeoutSeconds));

lifetime.ApplicationStopping.Register(() =>
    shutdownLogger.LogWarning(
        "SIGTERM received. Stopping new request acceptance. " +
        "Waiting up to {Timeout}s for in-flight requests to complete...",
        gracefulShutdownSettings.ShutdownTimeoutSeconds));

lifetime.ApplicationStopped.Register(() =>
    shutdownLogger.LogInformation("Application stopped gracefully."));

app.Run();

/// <summary>
/// Find the solution root directory by looking for .sln file or .env file.
/// </summary>
static string? FindSolutionRoot(string startPath)
{
    var dir = new DirectoryInfo(startPath);
    while (dir != null)
    {
        // Check for .sln file or .env file as markers of solution root
        if (dir.GetFiles("*.sln").Length > 0 || File.Exists(Path.Combine(dir.FullName, ".env")))
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    return null;
}
