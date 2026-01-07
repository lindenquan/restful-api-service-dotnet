# Observability

This document describes the built-in observability features of the API.

## System Metrics Logging

The `SystemMetricsService` is a background service that logs comprehensive system metrics:

- **At startup**: Logs baseline metrics when the application starts
- **Every 5 minutes**: Logs periodic metrics for monitoring system health

### Logged Metrics

| Category | Metric | Description |
|----------|--------|-------------|
| **Memory** | Heap Size | Managed heap memory in MB |
| | Working Set | Total process memory in MB |
| | Private Memory | Private memory allocation in MB |
| | Fragmented Bytes | GC fragmented memory in MB |
| **GC** | Gen0/Gen1/Gen2 | Collection counts per generation |
| **Threads** | Thread Count | Total process threads |
| | Handle Count | OS handle count |
| **Thread Pool** | Worker Threads | Busy/Max/Min worker threads |
| | IO Threads | Busy/Max/Min completion port threads |
| | Pending Work | Queued work items |
| **Process** | Uptime | Time since process started |
| | CPU Time | Total processor time consumed |

### Example Log Output

```
System Metrics [Startup] | Uptime: 00:00:02 | Heap: 45.3 MB | WorkingSet: 89.2 MB | 
PrivateMemory: 102.4 MB | GC (Gen0/1/2): 2/1/0 | Fragmented: 0.5 MB | Threads: 24 | 
Handles: 312 | ThreadPool Workers: 4/32767 (min:12) | ThreadPool IO: 1/1000 (min:12) | 
Pending Work: 0 | CPU Time: 00:00:00.234
```

### Debug-Level GC Details

At `Debug` log level, additional GC information is logged:

- Total available memory
- High memory load threshold
- Current memory load
- Compaction and concurrent GC status
- GC pause time percentage

### Why These Metrics Matter

For a healthcare prescription system, monitoring these metrics is critical:

| Metric | Warning Signs | Action |
|--------|--------------|--------|
| Heap size growing | Memory leak | Investigate allocation patterns |
| Gen2 collections increasing | Large object pressure | Review large object usage |
| Thread pool exhausted | Request queueing | Increase min threads or reduce async blocking |
| High fragmentation | LOH issues | Consider object pooling |
| CPU time high | Compute-heavy operations | Profile and optimize |

### Configuration

The service is automatically registered and requires no configuration. It runs as a hosted background service.

To disable, remove the service registration in `DependencyInjection`:

```csharp
// In Infrastructure/Api/DependencyInjection.cs
// Comment out or remove:
// services.AddHostedService<SystemMetricsService>();
```

### Integration with External Monitoring

These structured logs integrate well with:

- **Application Insights**: Properties extracted for dashboards
- **Elasticsearch/Kibana**: Indexed for time-series analysis
- **Prometheus**: Can be scraped via log exporters
- **Grafana**: Visualize trends over time

For production, consider setting up alerts on:

- Heap size > 80% of available memory
- Gen2 collections > threshold per hour
- Thread pool saturation (busy = max)
- Pending work items > threshold

