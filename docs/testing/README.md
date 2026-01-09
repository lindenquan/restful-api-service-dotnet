# Testing Documentation

This folder contains all testing-related documentation for the Prescription Order API.

## Test Types

| Test Type | Project | Scale | Purpose |
|-----------|---------|-------|---------|
| **Unit Tests** | `tests/Tests/` | N/A | Test business logic in isolation |
| **API E2E Tests** | `tests/Tests.Api.E2E/` | N/A | Test API endpoints against real DB |
| **Load Tests (Local)** | `tests/Tests.LoadTests/` | 100-1,000 users | Development, CI/CD, catching bugs |
| **Load Tests (Cloud)** | `tests/Tests.LoadTests.Azure/` | 1,000-1M+ users | Pre-release, capacity planning |

## Quick Start

```powershell
# 1. Start infrastructure
docker compose up -d

# 2. Run unit tests
dotnet test tests/Tests

# 3. Run E2E tests
dotnet test tests/Tests.Api.E2E

# 4. Run load tests (local - NBomber)
./build.ps1 test-load

# 5. Run load tests (cloud - Azure)
# Via GitHub Actions → "Azure Load Test" workflow
```

## Scale Expectations

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                  │
│  NBomber (Local)              Azure Load Testing (Cloud)        │
│  ───────────────              ──────────────────────────        │
│  100-1,000 users              1,000-1,000,000+ users            │
│                                                                  │
│  ✅ Development               ✅ Pre-release validation         │
│  ✅ CI/CD pipeline            ✅ Capacity planning              │
│  ✅ Race condition bugs       ✅ Production stress testing      │
│  ❌ Production scale          ✅ Multi-region testing           │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Documentation Index

| Document | Description |
|----------|-------------|
| [01-testing-strategy.md](./01-testing-strategy.md) | Overall testing strategy and philosophy |
| [02-e2e-testing.md](./02-e2e-testing.md) | API E2E testing guide |
| [03-load-concurrency-tests.md](./03-load-concurrency-tests.md) | Load and concurrency testing (NBomber) |
| [Azure Load Testing](../../tests/Tests.LoadTests.Azure/README.md) | Cloud-based load testing (1M+ users) |

## Testing Pyramid

```
                ┌─────────────────────┐
                │  Azure Load Testing │  ← Rare, expensive
                │  (1M+ users)        │     Production validation
                ├─────────────────────┤
                │  NBomber Load Tests │  ← Few, slow
                │  (100-1K users)     │     CI/CD, catch bottlenecks
                ├─────────────────────┤
                │    API E2E Tests    │  ← Some, medium speed
                │                     │     Test real integrations
                ├─────────────────────┤
                │                     │
                │    Unit Tests       │  ← Many, fast, cheap
                │                     │     Test business logic
                └─────────────────────┘
```

## CI/CD Integration

```yaml
# GitHub Actions workflow
jobs:
  test:
    steps:
      # Fast tests - every PR
      - name: Unit Tests
        run: dotnet test tests/Tests

      - name: Start infrastructure
        run: docker compose up -d

      - name: E2E Tests
        run: dotnet test tests/Tests.Api.E2E

      # NBomber - nightly (conservative CI mode)
      - name: Load Tests (NBomber)
        run: dotnet run --project tests/Tests.LoadTests -- --ci

  # Azure Load Testing - pre-release only (manual trigger)
  load-test-azure:
    if: github.event_name == 'workflow_dispatch'
    uses: ./.github/workflows/load-test-azure.yml
```

