using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Infrastructure.Resilience;

/// <summary>
/// Extension methods for registering resilience services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Add resilience pipelines for MongoDB, Redis, and HTTP clients.
    /// </summary>
    public static IServiceCollection AddResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(ResilienceSettings.SectionName)
            .Get<ResilienceSettings>() ?? new ResilienceSettings();

        services.AddSingleton(settings);

        // Register resilience pipelines
        services.AddResiliencePipeline(ResiliencePipelineNames.MongoDB, (builder, context) =>
        {
            ConfigurePipeline(builder, settings.MongoDB, "MongoDB", context.ServiceProvider);
        });

        services.AddResiliencePipeline(ResiliencePipelineNames.Redis, (builder, context) =>
        {
            ConfigurePipeline(builder, settings.Redis, "Redis", context.ServiceProvider);
        });

        services.AddResiliencePipeline(ResiliencePipelineNames.HttpClient, (builder, context) =>
        {
            ConfigurePipeline(builder, settings.HttpClient, "HttpClient", context.ServiceProvider);
        });

        // Register resilient executor for easy use in repositories
        services.AddSingleton<IResilientExecutor, ResilientExecutor>();

        return services;
    }

    private static void ConfigurePipeline(
        ResiliencePipelineBuilder builder,
        ServiceResilienceSettings settings,
        string serviceName,
        IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ResilienceSettings>>();

        // Add timeout (outermost - applies to entire operation including retries)
        if (settings.Timeout.Enabled)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Timeout.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "{Service} operation timed out after {Timeout}s",
                        serviceName,
                        settings.Timeout.TimeoutSeconds);
                    return ValueTask.CompletedTask;
                }
            });
        }

        // Add retry
        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = settings.Retry.MaxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(settings.Retry.BaseDelayMs),
            BackoffType = settings.Retry.GetBackoffType(),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(TransientExceptionHelper.IsTransient),
            OnRetry = args =>
            {
                logger.LogWarning(
                    args.Outcome.Exception,
                    "{Service} operation failed, retry attempt {Attempt}/{Max} after {Delay}ms",
                    serviceName,
                    args.AttemptNumber,
                    settings.Retry.MaxRetryAttempts,
                    args.RetryDelay.TotalMilliseconds);
                return ValueTask.CompletedTask;
            }
        });

        // Add circuit breaker
        if (settings.CircuitBreaker.Enabled)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = settings.CircuitBreaker.FailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(settings.CircuitBreaker.SamplingDurationSeconds),
                MinimumThroughput = settings.CircuitBreaker.MinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreaker.BreakDurationSeconds),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(TransientExceptionHelper.IsTransient),
                OnOpened = args =>
                {
                    logger.LogError(
                        args.Outcome.Exception,
                        "{Service} circuit breaker OPENED for {Duration}s due to failures",
                        serviceName,
                        settings.CircuitBreaker.BreakDurationSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("{Service} circuit breaker CLOSED - service recovered", serviceName);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("{Service} circuit breaker HALF-OPEN - testing service", serviceName);
                    return ValueTask.CompletedTask;
                }
            });
        }
    }
}

