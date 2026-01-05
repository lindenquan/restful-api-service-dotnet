using Polly;
using Polly.Registry;

namespace Infrastructure.Resilience;

/// <summary>
/// Provides resilient execution of operations using configured Polly pipelines.
/// </summary>
public interface IResilientExecutor
{
    /// <summary>
    /// Execute an operation with MongoDB resilience pipeline (retry + circuit breaker).
    /// </summary>
    Task<T> ExecuteMongoDbAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);

    /// <summary>
    /// Execute an operation with MongoDB resilience pipeline (retry + circuit breaker).
    /// </summary>
    Task ExecuteMongoDbAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);

    /// <summary>
    /// Execute an operation with Redis resilience pipeline (retry + circuit breaker).
    /// </summary>
    Task<T> ExecuteRedisAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);

    /// <summary>
    /// Execute an operation with Redis resilience pipeline (retry + circuit breaker).
    /// </summary>
    Task ExecuteRedisAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);

    /// <summary>
    /// Execute an operation with HTTP client resilience pipeline (retry + circuit breaker).
    /// </summary>
    Task<T> ExecuteHttpAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);
}

/// <summary>
/// Implementation of resilient executor using Polly pipelines.
/// </summary>
public sealed class ResilientExecutor : IResilientExecutor
{
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public ResilientExecutor(ResiliencePipelineProvider<string> pipelineProvider)
    {
        _pipelineProvider = pipelineProvider;
    }

    public async Task<T> ExecuteMongoDbAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        var pipeline = _pipelineProvider.GetPipeline(ResiliencePipelineNames.MongoDB);
        return await pipeline.ExecuteAsync(async token => await operation(token), ct);
    }

    public async Task ExecuteMongoDbAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        var pipeline = _pipelineProvider.GetPipeline(ResiliencePipelineNames.MongoDB);
        await pipeline.ExecuteAsync(async token =>
        {
            await operation(token);
        }, ct);
    }

    public async Task<T> ExecuteRedisAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        var pipeline = _pipelineProvider.GetPipeline(ResiliencePipelineNames.Redis);
        return await pipeline.ExecuteAsync(async token => await operation(token), ct);
    }

    public async Task ExecuteRedisAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        var pipeline = _pipelineProvider.GetPipeline(ResiliencePipelineNames.Redis);
        await pipeline.ExecuteAsync(async token =>
        {
            await operation(token);
        }, ct);
    }

    public async Task<T> ExecuteHttpAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        var pipeline = _pipelineProvider.GetPipeline(ResiliencePipelineNames.HttpClient);
        return await pipeline.ExecuteAsync(async token => await operation(token), ct);
    }
}

