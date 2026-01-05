using Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;

namespace Tests.Infrastructure.Resilience;

/// <summary>
/// Tests for ResilienceSettings configuration binding.
/// </summary>
public class ResilienceSettingsTests
{
    [Fact]
    public void ResilienceSettings_DefaultValues_AreReasonable()
    {
        // Arrange & Act
        var settings = new ResilienceSettings();

        // Assert - MongoDB defaults
        Assert.Equal(3, settings.MongoDB.Retry.MaxRetryAttempts);
        Assert.Equal(200, settings.MongoDB.Retry.BaseDelayMs);
        Assert.Equal("Exponential", settings.MongoDB.Retry.BackoffType);
        Assert.True(settings.MongoDB.CircuitBreaker.Enabled);
        Assert.Equal(0.5, settings.MongoDB.CircuitBreaker.FailureRatio);
        Assert.Equal(30, settings.MongoDB.Timeout.TimeoutSeconds);

        // Assert - Redis defaults
        Assert.Equal(3, settings.Redis.Retry.MaxRetryAttempts);
        Assert.True(settings.Redis.CircuitBreaker.Enabled);

        // Assert - HttpClient defaults
        Assert.Equal(3, settings.HttpClient.Retry.MaxRetryAttempts);
        Assert.True(settings.HttpClient.CircuitBreaker.Enabled);
    }

    [Fact]
    public void RetrySettings_GetBackoffType_ParsesExponential()
    {
        // Arrange
        var settings = new RetrySettings { BackoffType = "Exponential" };

        // Act
        var result = settings.GetBackoffType();

        // Assert
        Assert.Equal(Polly.DelayBackoffType.Exponential, result);
    }

    [Fact]
    public void RetrySettings_GetBackoffType_ParsesLinear()
    {
        // Arrange
        var settings = new RetrySettings { BackoffType = "Linear" };

        // Act
        var result = settings.GetBackoffType();

        // Assert
        Assert.Equal(Polly.DelayBackoffType.Linear, result);
    }

    [Fact]
    public void RetrySettings_GetBackoffType_ParsesConstant()
    {
        // Arrange
        var settings = new RetrySettings { BackoffType = "Constant" };

        // Act
        var result = settings.GetBackoffType();

        // Assert
        Assert.Equal(Polly.DelayBackoffType.Constant, result);
    }

    [Fact]
    public void RetrySettings_GetBackoffType_DefaultsToExponential()
    {
        // Arrange
        var settings = new RetrySettings { BackoffType = "Unknown" };

        // Act
        var result = settings.GetBackoffType();

        // Assert
        Assert.Equal(Polly.DelayBackoffType.Exponential, result);
    }

    [Fact]
    public void RetrySettings_GetBackoffType_CaseInsensitive()
    {
        // Arrange
        var settings = new RetrySettings { BackoffType = "CONSTANT" };

        // Act
        var result = settings.GetBackoffType();

        // Assert
        Assert.Equal(Polly.DelayBackoffType.Constant, result);
    }

    [Fact]
    public void ResilienceSettings_BindsFromConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resilience:MongoDB:Retry:MaxRetryAttempts"] = "5",
                ["Resilience:MongoDB:Retry:BaseDelayMs"] = "500",
                ["Resilience:MongoDB:CircuitBreaker:Enabled"] = "false",
                ["Resilience:MongoDB:Timeout:TimeoutSeconds"] = "60"
            })
            .Build();

        // Act
        var settings = config.GetSection(ResilienceSettings.SectionName).Get<ResilienceSettings>();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(5, settings.MongoDB.Retry.MaxRetryAttempts);
        Assert.Equal(500, settings.MongoDB.Retry.BaseDelayMs);
        Assert.False(settings.MongoDB.CircuitBreaker.Enabled);
        Assert.Equal(60, settings.MongoDB.Timeout.TimeoutSeconds);
    }

    [Fact]
    public void CircuitBreakerSettings_DefaultValues()
    {
        // Arrange & Act
        var settings = new CircuitBreakerSettings();

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(0.5, settings.FailureRatio);
        Assert.Equal(10, settings.SamplingDurationSeconds);
        Assert.Equal(10, settings.MinimumThroughput);
        Assert.Equal(30, settings.BreakDurationSeconds);
    }

    [Fact]
    public void TimeoutSettings_DefaultValues()
    {
        // Arrange & Act
        var settings = new TimeoutSettings();

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(30, settings.TimeoutSeconds);
    }
}

