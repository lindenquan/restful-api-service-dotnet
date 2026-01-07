namespace Infrastructure.Api.Configuration;

/// <summary>
/// Configuration for graceful shutdown behavior.
/// 
/// In Kubernetes, the shutdown sequence is:
/// 1. Pod receives SIGTERM
/// 2. Pod is removed from Service endpoints (no new traffic)
/// 3. Application has terminationGracePeriodSeconds to shut down gracefully
/// 4. If still running, SIGKILL is sent
/// 
/// ShutdownTimeoutSeconds should be LESS than Kubernetes terminationGracePeriodSeconds
/// to ensure clean shutdown before forceful termination.
/// </summary>
public sealed class GracefulShutdownSettings
{
    public const string SectionName = "GracefulShutdown";

    /// <summary>
    /// Maximum time in seconds to wait for in-flight requests to complete during shutdown.
    /// Should be less than Kubernetes terminationGracePeriodSeconds (default 30s, we use 60s).
    /// Default: 55 seconds (leaving 5s buffer before K8s sends SIGKILL at 60s).
    /// </summary>
    public int ShutdownTimeoutSeconds { get; set; } = 55;
}

