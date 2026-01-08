# Load & Concurrency Testing

## Overview

This project provides **two tiers** of load testing:

| Tier | Tool | Scale | Use Case |
|------|------|-------|----------|
| **Local** | NBomber | 100-1,000 users | Development, CI/CD, catching bugs |
| **Cloud** | Azure Load Testing | 1,000-1,000,000+ users | Pre-release, capacity planning |

## Scale Expectations

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         LOAD TESTING SCALE                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  NBomber (Local)                  Azure Load Testing (Cloud)                │
│  ───────────────                  ──────────────────────────                │
│                                                                              │
│  ┌─────────────┐                  ┌─────────────────────────────────────┐   │
│  │  Your PC    │                  │     Azure Infrastructure            │   │
│  │  4-8 GB RAM │                  │  ┌────────┐ ┌────────┐ ┌────────┐  │   │
│  │  4+ cores   │                  │  │Engine 1│ │Engine 2│ │Engine N│  │   │
│  └─────────────┘                  │  └────────┘ └────────┘ └────────┘  │   │
│        │                          └─────────────────────────────────────┘   │
│        ▼                                         │                          │
│  100-1,000 users                         1,000-1,000,000+ users             │
│                                                                              │
│  ✅ Development          ❌              ✅ Pre-release validation          │
│  ✅ CI/CD pipeline       ❌              ✅ Capacity planning               │
│  ✅ Race conditions      ❌              ✅ Multi-region testing            │
│  ❌ Production scale     ❌              ✅ Stress testing to failure       │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## NBomber (Local) - What We Test

| Test Type | Question | Measures | Pass Criteria |
|-----------|----------|----------|---------------|
| **Load Test** | Can we handle 500 req/sec? | Throughput, p95 latency | p95 < 500ms, errors < 1% |
| **Concurrency Test** | Is data corrupted? | Data integrity | No duplicates, no corruption |

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    LOAD TEST vs CONCURRENCY TEST                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  LOAD TEST                        CONCURRENCY TEST                          │
│  ───────────                      ────────────────                          │
│  "How fast?"                      "How safe?"                               │
│                                                                              │
│  ┌─────────┐                      ┌─────────┐                               │
│  │ Request │──► Measure latency   │ Request │──► Create order               │
│  │ Request │──► Measure latency   │ Request │──► Create order               │
│  │ Request │──► Measure latency   │ Request │──► Create order               │
│  └─────────┘                      └─────────┘                               │
│       │                                │                                     │
│       ▼                                ▼                                     │
│  ┌─────────────────┐              ┌─────────────────┐                       │
│  │ p95 < 500ms? ✓  │              │ All IDs unique? │                       │
│  │ Errors < 1%? ✓  │              │ No corruption?  │                       │
│  └─────────────────┘              └─────────────────┘                       │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Quick Start

### NBomber (Local)

```powershell
# 1. Start infrastructure + API
docker compose up -d

# 2. Run all tests (default: aggressive settings)
./build.ps1 test-load

# 3. Run only load tests
./build.ps1 test-load -LoadTestArgs load

# 4. Run only concurrency tests
./build.ps1 test-load -LoadTestArgs concurrency

# 5. CI mode (conservative, for pipelines)
dotnet run --project tests/Tests.LoadTests -- --ci

# 6. Stress mode (very aggressive)
dotnet run --project tests/Tests.LoadTests -- --stress
```

### Azure Load Testing (Cloud)

For production-scale testing (1,000+ users), use Azure Load Testing:

```powershell
# Via GitHub Actions (recommended)
# Go to Actions → "Azure Load Test" → Run workflow

# Or via Azure CLI
az load test-run create \
    --load-test-resource prescription-api-loadtest \
    --resource-group rg-loadtesting \
    --test-id prescription-api-load-test
```

See [tests/Tests.LoadTests.Azure/README.md](../../tests/Tests.LoadTests.Azure/README.md) for setup.

## Configuration

### Default Settings (Aggressive)

| Setting | Default | CI Mode | Stress Mode |
|---------|---------|---------|-------------|
| Duration | 60s | 30s | 120s |
| Requests/sec | 500 | 100 | 1000 |
| Concurrent | 200 | 50 | 500 |
| Ramp-up | 10s | 5s | 15s |

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `API_BASE_URL` | `http://localhost:8080` | API endpoint |
| `API_KEY` | `root-api-key-...` | Admin API key |
| `LOAD_TEST_DURATION` | `60` | Test duration in seconds |
| `LOAD_TEST_RPS` | `500` | Requests per second |
| `LOAD_TEST_CONCURRENT` | `200` | Concurrent requests |
| `LOAD_TEST_RAMP_UP` | `10` | Ramp-up time in seconds |

```powershell
# Example: Custom configuration
$env:API_BASE_URL = "http://localhost:8080"
$env:LOAD_TEST_RPS = "200"
$env:LOAD_TEST_DURATION = "60"
dotnet run --project tests/Tests.LoadTests
```

## Test Scenarios

### Load Tests

| Scenario | Description | What It Tests |
|----------|-------------|---------------|
| `load_test_reads` | GET /api/v1/orders | Read throughput |
| `load_test_writes` | POST /api/v1/orders | Write throughput |
| `load_test_mixed` | 80% reads, 20% writes | Realistic workload |

### Concurrency Tests

| Scenario | Description | What It Verifies |
|----------|-------------|------------------|
| `concurrency_test_writes` | Parallel order creation | No duplicate IDs |
| `concurrency_test_read_after_write` | Create then immediately read | No stale cache reads |
| `concurrency_test_cache_stampede` | Many requests for same uncached data | Graceful handling |

## Output

NBomber generates reports in `tests/Tests.LoadTests/reports/`:

```
╭─────────────────────────────────────────────────────────────────────╮
│                          load test report                            │
├─────────────────────────────────────────────────────────────────────┤
│ scenario: load_test_reads                                            │
├─────────────────────────────────────────────────────────────────────┤
│ ok count:        2847      fail count:     3                        │
│ requests/sec:    94.9      latency p50:    45ms                     │
│ latency p75:     67ms      latency p95:    125ms                    │
│ latency p99:     230ms     data received:  1.4 MB                   │
╰─────────────────────────────────────────────────────────────────────╯

[DATA INTEGRITY] Created: 500, Unique: 500
[DATA INTEGRITY] ✅ No duplicate IDs
```

## Project Structure

```
tests/Tests.LoadTests/
├── Tests.LoadTests.csproj
├── Program.cs                    # Entry point
├── Configuration/
│   └── LoadTestSettings.cs       # Environment-based config
└── Scenarios/
    ├── LoadTestScenarios.cs      # Throughput & latency tests
    └── ConcurrencyTestScenarios.cs # Data integrity tests
```

## Why NBomber?

| Feature | NBomber | k6 |
|---------|---------|-----|
| **Language** | C# | JavaScript |
| **Share DTOs** | ✅ Yes | ❌ No |
| **IDE Support** | Full IntelliSense | Basic |
| **Debugging** | Standard .NET | Limited |
| **CI/CD** | `dotnet run` | Separate install |

## CI/CD Integration

### NBomber (Nightly, Local Runner)

```yaml
# .github/workflows/load-tests.yml
name: Load Tests (NBomber)

on:
  schedule:
    - cron: '0 2 * * *'  # Nightly at 2 AM

jobs:
  load-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Start services
        run: docker compose up -d

      - name: Wait for healthy
        run: sleep 15

      - name: Run load tests (CI mode)
        run: dotnet run --project tests/Tests.LoadTests -- --ci

      - name: Upload report
        uses: actions/upload-artifact@v4
        with:
          name: load-test-report
          path: tests/Tests.LoadTests/reports/
```

### Azure Load Testing (Pre-release, Cloud)

```yaml
# .github/workflows/load-test-azure.yml
# See: .github/workflows/load-test-azure.yml for full example

on:
  workflow_dispatch:
    inputs:
      virtual_users:
        description: 'Number of virtual users'
        default: '1000'

jobs:
  load-test:
    runs-on: ubuntu-latest
    steps:
      - uses: azure/load-testing@v1
        with:
          loadTestConfigFile: tests/Tests.LoadTests.Azure/load-test-config.yaml
          # ... see full workflow for details
```

## When to Use What

| Scenario | NBomber (Local) | Azure Load Testing |
|----------|-----------------|-------------------|
| Development | ✅ | ❌ |
| PR checks | ✅ (--ci mode) | ❌ |
| Nightly builds | ✅ | ❌ |
| Pre-release | ❌ | ✅ |
| Production validation | ❌ | ✅ |
| Capacity planning | ❌ | ✅ |
| Multi-region testing | ❌ | ✅ |

## Related Documentation

- [Azure Load Testing Setup](../../tests/Tests.LoadTests.Azure/README.md)
- [Testing Strategy](./01-testing-strategy.md)
- [E2E Testing](./02-e2e-testing.md)
