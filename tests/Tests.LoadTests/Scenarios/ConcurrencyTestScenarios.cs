using System.Collections.Concurrent;
using System.Net.Http.Json;
using DTOs.V1;
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
    /// Tests that concurrent order creation doesn't produce duplicate IDs or data corruption.
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
                var request = new CreateOrderRequest(
                    PatientId: testPatientId,
                    PrescriptionId: testPrescriptionId,
                    Notes: $"ConcurrencyTest-{context.InvocationNumber}-{Guid.NewGuid():N}"
                );

                var response = await client.PostAsJsonAsync("/api/v1/orders", request);

                if (response.IsSuccessStatusCode)
                {
                    var created = await response.Content.ReadFromJsonAsync<OrderDto>();
                    if (created != null)
                    {
                        createdIds.Add(created.Id);
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
    /// Creates an order then immediately reads it - verifies no stale cache reads.
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
                // 1. Create order
                var request = new CreateOrderRequest(
                    PatientId: testPatientId,
                    PrescriptionId: testPrescriptionId,
                    Notes: $"ReadAfterWrite-{context.InvocationNumber}"
                );

                var createResponse = await client.PostAsJsonAsync("/api/v1/orders", request);
                if (!createResponse.IsSuccessStatusCode)
                {
                    return Response.Fail(statusCode: ((int)createResponse.StatusCode).ToString());
                }

                var created = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
                if (created == null)
                {
                    return Response.Fail(message: "Failed to deserialize created order");
                }

                // 2. Immediately read it back
                var getResponse = await client.GetAsync($"/api/v1/orders/{created.Id}");
                if (!getResponse.IsSuccessStatusCode)
                {
                    consistencyResults.Add(false);
                    return Response.Fail(message: "Read after write failed");
                }

                var retrieved = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
                var isConsistent = retrieved?.Id == created.Id && retrieved?.Notes == created.Notes;
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

