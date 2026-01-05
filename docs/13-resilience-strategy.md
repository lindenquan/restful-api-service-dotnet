# Resilience Strategy

This document describes the resilience patterns implemented in the API to handle transient failures gracefully.

## Overview

The API uses [Polly](https://github.com/App-vNext/Polly) via `Microsoft.Extensions.Resilience` to implement:

- **Retry policies** - Automatic retry with exponential backoff for transient failures
- **Circuit breakers** - Prevent cascading failures by temporarily stopping requests to failing services
- **Timeouts** - Prevent operations from hanging indefinitely

## Configuration

Resilience settings are configured in `appsettings.json`:

```json
{
  "Resilience": {
    "MongoDB": {
      "Retry": {
        "MaxRetryAttempts": 3,
        "BaseDelayMs": 200,
        "BackoffType": "Exponential"
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureRatio": 0.5,
        "SamplingDurationSeconds": 10,
        "MinimumThroughput": 10,
        "BreakDurationSeconds": 30
      },
      "Timeout": {
        "Enabled": true,
        "TimeoutSeconds": 30
      }
    },
    "Redis": {
      "Retry": {
        "MaxRetryAttempts": 2,
        "BaseDelayMs": 100,
        "BackoffType": "Exponential"
      },
      "CircuitBreaker": {
        "Enabled": true,
        "FailureRatio": 0.5,
        "SamplingDurationSeconds": 10,
        "MinimumThroughput": 20,
        "BreakDurationSeconds": 15
      },
      "Timeout": {
        "Enabled": true,
        "TimeoutSeconds": 5
      }
    }
  }
}
```

## Retry Policy

The retry policy handles transient failures by automatically retrying failed operations:

| Setting | Description |
|---------|-------------|
| `MaxRetryAttempts` | Maximum number of retry attempts before giving up |
| `BaseDelayMs` | Initial delay between retries in milliseconds |
| `BackoffType` | `Constant`, `Linear`, or `Exponential` backoff |

### Exponential Backoff Example

With `BaseDelayMs: 200` and `BackoffType: Exponential`:
- Retry 1: 200ms delay
- Retry 2: 400ms delay  
- Retry 3: 800ms delay

## Circuit Breaker

The circuit breaker prevents cascading failures by temporarily stopping requests to a failing service:

| Setting | Description |
|---------|-------------|
| `Enabled` | Whether circuit breaker is active |
| `FailureRatio` | Failure percentage threshold (0.0-1.0) to trip the circuit |
| `SamplingDurationSeconds` | Time window for calculating failure ratio |
| `MinimumThroughput` | Minimum requests before circuit can trip |
| `BreakDurationSeconds` | How long circuit stays open before testing |

### Circuit States

1. **Closed** - Normal operation, requests flow through
2. **Open** - Circuit tripped, requests fail immediately
3. **Half-Open** - Testing if service recovered

## Transient Exceptions

The following exceptions are considered transient and will trigger retries:

### MongoDB
- `MongoConnectionException`
- `MongoExecutionTimeoutException`
- `MongoCursorNotFoundException`

### Redis
- `RedisConnectionException`
- `RedisTimeoutException`
- `RedisServerException` (when message contains "BUSY")

### Network
- `SocketException`
- `IOException`
- `TimeoutException`

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      API Request                            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Timeout Policy                           │
│              (Outermost - applies to all)                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Retry Policy                            │
│           (Retries on transient failures)                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Circuit Breaker                            │
│         (Prevents cascading failures)                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                MongoDB / Redis Operation                    │
└─────────────────────────────────────────────────────────────┘
```

## Usage

Resilience is automatically applied to all MongoDB repository operations through the `IResilientExecutor`:

```csharp
public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
{
    return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
    {
        var filter = Builders<T>.Filter.Eq(e => e.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(token);
    }, ct);
}
```

## Logging

All resilience events are logged:

- **Retry attempts** - Warning level with attempt number and delay
- **Circuit opened** - Error level with break duration
- **Circuit closed** - Information level
- **Timeouts** - Warning level

## Best Practices

1. **Don't retry non-idempotent operations** - Only retry read operations or idempotent writes
2. **Set appropriate timeouts** - Prevent requests from hanging indefinitely
3. **Monitor circuit breaker state** - Use health checks to detect open circuits
4. **Tune thresholds** - Adjust based on your service's normal failure rate

