using System.Collections.Concurrent;
using System.Net.Http.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using Tests.LoadTests.Configuration;

namespace Tests.LoadTests.Scenarios;

/// <summary>
/// Concurrency test scenarios - verify data integrity under parallel access.
/// These tests answer: "Is data safe when multiple users access simultaneously?"
/// </summary>
public static class ConcurrencyTestScenarios
{
    /// <summary>
    /// Tests that concurrent patient creation doesn't produce duplicate IDs or data corruption.
    /// Uses patient creation instead of orders since orders consume prescription refills.
    /// Collects all created IDs and verifies uniqueness after the test.
    /// </summary>
    public static (ScenarioProps Scenario, ConcurrentBag<Guid> CreatedIds) CreateConcurrentWritesScenario(
        HttpClient client,
        LoadTestSettings settings,
        Guid testPatientId,
        Guid testPrescriptionId)
    {
        var createdIds = new ConcurrentBag<Guid>();

        var scenario = Scenario.Create("concurrency_test_writes", async context =>
        {
            try
            {
                // Use patient creation for concurrent write tests since orders consume limited refills
                var request = new
                {
                    firstName = "LoadTest",
                    lastName = $"Patient{context.InvocationNumber}",
                    email = $"loadtest.{Guid.NewGuid():N}@example.com",
                    phone = "555-0100",
                    dateOfBirth = DateTime.UtcNow.AddYears(-30).ToString("yyyy-MM-dd")
                };

                var response = await client.PostAsJsonAsync("/api/v1/patients", request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    if (json.TryGetProperty("id", out var idElement) &&
                        idElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                        Guid.TryParse(idElement.GetString(), out var id))
                    {
                        createdIds.Add(id);
                    }
                    return Response.Ok(statusCode: ((int)response.StatusCode).ToString());
                }

                return Response.Fail(statusCode: ((int)response.StatusCode).ToString());
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(
                rate: settings.ConcurrentRequests,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(10)) // Shorter duration for concurrency tests
        );

        return (scenario, createdIds);
    }

    /// <summary>
    /// Tests cache stampede scenario - many concurrent requests for the same uncached data.
    /// Verifies that the system handles cache miss storms gracefully.
    /// </summary>
    public static ScenarioProps CreateCacheStampedeScenario(
        HttpClient client,
        LoadTestSettings settings,
        Guid orderId)
    {
        return Scenario.Create("concurrency_test_cache_stampede", async context =>
        {
            try
            {
                // All requests hit the same resource simultaneously
                var response = await client.GetAsync($"/api/v1/orders/{orderId}");

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
            // Burst of concurrent requests
            Simulation.Inject(
                rate: settings.ConcurrentRequests * 2,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(5))
        );
    }

    /// <summary>
    /// Tests concurrent read-after-write consistency.
    /// Creates a patient then immediately reads it - verifies no stale cache reads.
    /// Uses patient creation instead of orders since orders consume prescription refills.
    /// </summary>
    public static (ScenarioProps Scenario, ConcurrentBag<bool> ConsistencyResults) CreateReadAfterWriteScenario(
        HttpClient client,
        LoadTestSettings settings,
        Guid testPatientId,
        Guid testPrescriptionId)
    {
        var consistencyResults = new ConcurrentBag<bool>();

        var scenario = Scenario.Create("concurrency_test_read_after_write", async context =>
        {
            try
            {
                // 1. Create patient (instead of order to avoid refill limits)
                var uniqueEmail = $"raw.{Guid.NewGuid():N}@example.com";
                var request = new
                {
                    firstName = "ReadAfterWrite",
                    lastName = $"Test{context.InvocationNumber}",
                    email = uniqueEmail,
                    phone = "555-0100",
                    dateOfBirth = DateTime.UtcNow.AddYears(-30).ToString("yyyy-MM-dd")
                };

                var createResponse = await client.PostAsJsonAsync("/api/v1/patients", request);
                if (!createResponse.IsSuccessStatusCode)
                {
                    return Response.Fail(statusCode: ((int)createResponse.StatusCode).ToString());
                }

                var json = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                if (!json.TryGetProperty("id", out var idElement) ||
                    idElement.ValueKind != System.Text.Json.JsonValueKind.String ||
                    !Guid.TryParse(idElement.GetString(), out var createdId))
                {
                    return Response.Fail(message: "Failed to deserialize created patient");
                }

                // 2. Immediately read it back
                var getResponse = await client.GetAsync($"/api/v1/patients/{createdId}");
                if (!getResponse.IsSuccessStatusCode)
                {
                    consistencyResults.Add(false);
                    return Response.Fail(message: "Read after write failed");
                }

                var retrievedJson = await getResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                var isConsistent = retrievedJson.TryGetProperty("id", out var retrievedIdElement) &&
                                   retrievedIdElement.GetString() == createdId.ToString() &&
                                   retrievedJson.TryGetProperty("email", out var emailElement) &&
                                   emailElement.GetString() == uniqueEmail;
                consistencyResults.Add(isConsistent);

                return isConsistent
                    ? Response.Ok()
                    : Response.Fail(message: "Data inconsistency detected");
            }
            catch (Exception ex)
            {
                consistencyResults.Add(false);
                return Response.Fail(message: ex.Message);
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(
                rate: settings.ConcurrentRequests / 2,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(10))
        );

        return (scenario, consistencyResults);
    }
}

