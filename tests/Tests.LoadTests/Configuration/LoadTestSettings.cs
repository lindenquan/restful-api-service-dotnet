namespace Tests.LoadTests.Configuration;

/// <summary>
/// Settings for load tests.
/// Can be configured via environment variables or command-line arguments.
///
/// SCALE EXPECTATIONS:
/// - NBomber (local): 100-1,000 concurrent users (development/CI)
/// - Azure Load Testing: 1,000-1,000,000+ users (production validation)
///
/// Default settings are aggressive for catching performance issues early.
/// </summary>
public sealed class LoadTestSettings
{
    /// <summary>
    /// Base URL of the API to test.
    /// Default: http://localhost:8080 (Docker Compose)
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:8080";

    /// <summary>
    /// Admin API key for authenticated requests.
    /// </summary>
    public string ApiKey { get; init; } = "root-api-key-change-in-production-12345";

    /// <summary>
    /// Duration of each load test scenario in seconds.
    /// Default: 60 seconds (enough to see steady-state behavior)
    /// </summary>
    public int DurationSeconds { get; init; } = 60;

    /// <summary>
    /// Number of requests per second for load tests.
    /// Default: 500 RPS (aggressive, will stress most local setups)
    ///
    /// Guidelines:
    /// - 100 RPS: Light load, good for CI/CD pipelines
    /// - 500 RPS: Moderate load, catches most bottlenecks
    /// - 1000+ RPS: Heavy load, requires beefy machine
    /// </summary>
    public int RequestsPerSecond { get; init; } = 500;

    /// <summary>
    /// Number of concurrent requests for concurrency tests.
    /// Default: 200 concurrent (aggressive, tests connection pooling)
    ///
    /// Guidelines:
    /// - 50: Light concurrency
    /// - 200: Moderate concurrency, catches most race conditions
    /// - 500+: Heavy concurrency, tests extreme scenarios
    /// </summary>
    public int ConcurrentRequests { get; init; } = 200;

    /// <summary>
    /// Ramp-up time in seconds (time to reach full load).
    /// Default: 10 seconds
    /// </summary>
    public int RampUpSeconds { get; init; } = 10;

    /// <summary>
    /// Creates settings from environment variables.
    /// All settings can be overridden via environment variables.
    /// </summary>
    public static LoadTestSettings FromEnvironment()
    {
        return new LoadTestSettings
        {
            BaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:8080",
            ApiKey = Environment.GetEnvironmentVariable("API_KEY") ?? "root-api-key-change-in-production-12345",
            DurationSeconds = int.TryParse(Environment.GetEnvironmentVariable("LOAD_TEST_DURATION"), out var duration)
                ? duration
                : 60,
            RequestsPerSecond = int.TryParse(Environment.GetEnvironmentVariable("LOAD_TEST_RPS"), out var rps)
                ? rps
                : 500,
            ConcurrentRequests = int.TryParse(Environment.GetEnvironmentVariable("LOAD_TEST_CONCURRENT"), out var concurrent)
                ? concurrent
                : 200,
            RampUpSeconds = int.TryParse(Environment.GetEnvironmentVariable("LOAD_TEST_RAMP_UP"), out var rampUp)
                ? rampUp
                : 10
        };
    }

    /// <summary>
    /// Creates conservative settings for CI/CD pipelines.
    /// Lower load to avoid flaky tests in resource-constrained environments.
    /// </summary>
    public static LoadTestSettings ForCI()
    {
        return new LoadTestSettings
        {
            DurationSeconds = 30,
            RequestsPerSecond = 100,
            ConcurrentRequests = 50,
            RampUpSeconds = 5
        };
    }

    /// <summary>
    /// Creates aggressive settings for local stress testing.
    /// Use when you want to push the system hard.
    /// </summary>
    public static LoadTestSettings ForStressTest()
    {
        return new LoadTestSettings
        {
            DurationSeconds = 120,
            RequestsPerSecond = 1000,
            ConcurrentRequests = 500,
            RampUpSeconds = 15
        };
    }
}

