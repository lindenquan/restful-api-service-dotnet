using System.Net.Http.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using Tests.LoadTests.Configuration;

namespace Tests.LoadTests.Scenarios;

/// <summary>
/// Load test scenarios - measure throughput, latency, and error rates under load.
/// These tests answer: "Can we handle X requests per second?"
/// Uses patient creation for writes since orders consume limited prescription refills.
/// </summary>
public static class LoadTestScenarios
{
    /// <summary>
    /// Tests read performance under load.
    /// Measures: throughput, p50/p95/p99 latency, error rate.
    /// </summary>
    public static ScenarioProps CreateReadLoadScenario(HttpClient client, LoadTestSettings settings)
    {
        return Scenario.Create("load_test_reads", async context =>
        {
            try
            {
                var response = await client.GetAsync("/api/v1/patients?$top=10");

                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: ((int)response.StatusCode).ToString())
                    : Response.Fail(statusCode: ((int)response.StatusCode).ToString());
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(
                rate: settings.RequestsPerSecond,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(settings.DurationSeconds))
        );
    }

    /// <summary>
    /// Tests write performance under load.
    /// Uses patient creation since orders consume limited prescription refills.
    /// Measures: throughput, latency for creating new patients.
    /// </summary>
    public static ScenarioProps CreateWriteLoadScenario(
        HttpClient client,
        LoadTestSettings settings,
        Guid testPatientId,
        Guid testPrescriptionId)
    {
        return Scenario.Create("load_test_writes", async context =>
        {
            try
            {
                var request = new
                {
                    firstName = "LoadTest",
                    lastName = $"Write{context.InvocationNumber}",
                    email = $"loadtest.write.{Guid.NewGuid():N}@example.com",
                    phone = "555-0100",
                    dateOfBirth = DateTime.UtcNow.AddYears(-30).ToString("yyyy-MM-dd")
                };

                var response = await client.PostAsJsonAsync("/api/v1/patients", request);

                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: ((int)response.StatusCode).ToString())
                    : Response.Fail(statusCode: ((int)response.StatusCode).ToString());
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(
                rate: settings.RequestsPerSecond / 2, // Lower rate for writes
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(settings.DurationSeconds))
        );
    }

    /// <summary>
    /// Tests mixed workload (80% reads, 20% writes).
    /// Uses patient creation for writes since orders consume limited prescription refills.
    /// More realistic traffic pattern.
    /// </summary>
    public static ScenarioProps CreateMixedWorkloadScenario(
        HttpClient client,
        LoadTestSettings settings,
        Guid testPatientId,
        Guid testPrescriptionId)
    {
        return Scenario.Create("load_test_mixed", async context =>
        {
            try
            {
                // 80% reads, 20% writes
                var isRead = context.InvocationNumber % 5 != 0;

                if (isRead)
                {
                    var response = await client.GetAsync("/api/v1/patients?$top=10");
                    return response.IsSuccessStatusCode
                        ? Response.Ok(statusCode: ((int)response.StatusCode).ToString())
                        : Response.Fail(statusCode: ((int)response.StatusCode).ToString());
                }
                else
                {
                    var request = new
                    {
                        firstName = "LoadTest",
                        lastName = $"Mixed{context.InvocationNumber}",
                        email = $"loadtest.mixed.{Guid.NewGuid():N}@example.com",
                        phone = "555-0100",
                        dateOfBirth = DateTime.UtcNow.AddYears(-30).ToString("yyyy-MM-dd")
                    };

                    var response = await client.PostAsJsonAsync("/api/v1/patients", request);
                    return response.IsSuccessStatusCode
                        ? Response.Ok(statusCode: ((int)response.StatusCode).ToString())
                        : Response.Fail(statusCode: ((int)response.StatusCode).ToString());
                }
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Ramp up to full load
            Simulation.RampingInject(
                rate: settings.RequestsPerSecond,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(settings.RampUpSeconds)),
            // Sustain full load
            Simulation.Inject(
                rate: settings.RequestsPerSecond,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(settings.DurationSeconds)),
            // Ramp down
            Simulation.RampingInject(
                rate: 0,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(settings.RampUpSeconds / 2))
        );
    }
}

