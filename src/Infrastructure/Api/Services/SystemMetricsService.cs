using System.Diagnostics;

namespace Infrastructure.Api.Services;

/// <summary>
/// Background service that logs system metrics (memory, GC, threads, CPU) at startup
/// and periodically. Critical for monitoring healthcare prescription system health.
/// </summary>
public sealed class SystemMetricsService : BackgroundService
{
    private readonly ILogger<SystemMetricsService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private readonly Process _currentProcess;

    public SystemMetricsService(ILogger<SystemMetricsService> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Log metrics at startup
        LogMetrics("Startup");

        // Then log every 5 minutes
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            LogMetrics("Periodic");
        }
    }

    private void LogMetrics(string trigger)
    {
        try
        {
            // Refresh process info
            _currentProcess.Refresh();

            // GC and memory metrics
            var gcInfo = GC.GetGCMemoryInfo();
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);
            var totalMemory = GC.GetTotalMemory(forceFullCollection: false);
            var heapSize = gcInfo.HeapSizeBytes;
            var fragmentedBytes = gcInfo.FragmentedBytes;

            // Process metrics
            var workingSet = _currentProcess.WorkingSet64;
            var privateMemory = _currentProcess.PrivateMemorySize64;
            var totalProcessorTime = _currentProcess.TotalProcessorTime;
            var threadCount = _currentProcess.Threads.Count;
            var handleCount = _currentProcess.HandleCount;
            var uptime = DateTime.UtcNow - _currentProcess.StartTime.ToUniversalTime();

            // Thread pool metrics
            ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableIoThreads);
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxIoThreads);
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minIoThreads);
            var busyWorkerThreads = maxWorkerThreads - availableWorkerThreads;
            var busyIoThreads = maxIoThreads - availableIoThreads;
            var pendingWorkItems = ThreadPool.PendingWorkItemCount;

            _logger.LogInformation(
                "System Metrics [{Trigger}] | " +
                "Uptime: {Uptime:hh\\:mm\\:ss} | " +
                "Heap: {HeapMB:F1} MB | " +
                "WorkingSet: {WorkingSetMB:F1} MB | " +
                "PrivateMemory: {PrivateMemoryMB:F1} MB | " +
                "GC (Gen0/1/2): {Gen0}/{Gen1}/{Gen2} | " +
                "Fragmented: {FragmentedMB:F1} MB | " +
                "Threads: {ThreadCount} | " +
                "Handles: {HandleCount} | " +
                "ThreadPool Workers: {BusyWorkers}/{MaxWorkers} (min:{MinWorkers}) | " +
                "ThreadPool IO: {BusyIo}/{MaxIo} (min:{MinIo}) | " +
                "Pending Work: {PendingWork} | " +
                "CPU Time: {CpuTime:hh\\:mm\\:ss\\.fff}",
                trigger,
                uptime,
                heapSize / (1024.0 * 1024.0),
                workingSet / (1024.0 * 1024.0),
                privateMemory / (1024.0 * 1024.0),
                gen0Collections,
                gen1Collections,
                gen2Collections,
                fragmentedBytes / (1024.0 * 1024.0),
                threadCount,
                handleCount,
                busyWorkerThreads,
                maxWorkerThreads,
                minWorkerThreads,
                busyIoThreads,
                maxIoThreads,
                minIoThreads,
                pendingWorkItems,
                totalProcessorTime);

            // Log detailed GC info at debug level
            _logger.LogDebug(
                "GC Details | " +
                "TotalAvailableMemory: {TotalAvailableMB:F1} MB | " +
                "HighMemoryLoadThreshold: {HighMemoryMB:F1} MB | " +
                "MemoryLoadBytes: {MemoryLoadMB:F1} MB | " +
                "Compacted: {Compacted} | " +
                "Concurrent: {Concurrent} | " +
                "PauseTimePercentage: {PausePercent:F2}%",
                gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0),
                gcInfo.HighMemoryLoadThresholdBytes / (1024.0 * 1024.0),
                gcInfo.MemoryLoadBytes / (1024.0 * 1024.0),
                gcInfo.Compacted,
                gcInfo.Concurrent,
                gcInfo.PauseTimePercentage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect system metrics");
        }
    }

    public override void Dispose()
    {
        _currentProcess.Dispose();
        base.Dispose();
    }
}

