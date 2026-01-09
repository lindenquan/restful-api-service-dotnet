using System.Net.Http.Json;
using DTOs.V1;
using NBomber.CSharp;
using Shouldly;
using Tests.LoadTests.Configuration;
using Tests.LoadTests.Scenarios;

Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    LOAD & CONCURRENCY TESTS                        ║");
Console.WriteLine("║                                                                    ║");
Console.WriteLine("║  SCALE: Local NBomber (100-1,000 users)                           ║");
Console.WriteLine("║  For millions of users, use Azure Load Testing                    ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

// Parse arguments
var testType = "all";
var useCI = false;
var useStress = false;

foreach (var arg in args)
{
    switch (arg.ToLowerInvariant())
    {
        case "load":
            testType = "load";
            break;
        case "concurrency":
            testType = "concurrency";
            break;
        case "--ci":
            useCI = true;
            break;
        case "--stress":
            useStress = true;
            break;
    }
}

// Load settings based on mode
var settings = useCI ? LoadTestSettings.ForCI()
    : useStress ? LoadTestSettings.ForStressTest()
    : LoadTestSettings.FromEnvironment();

Console.WriteLine();
Console.WriteLine($"Mode: {(useCI ? "CI (conservative)" : useStress ? "STRESS (aggressive)" : "Default")}");
Console.WriteLine($"Target: {settings.BaseUrl}");
Console.WriteLine($"Duration: {settings.DurationSeconds}s | RPS: {settings.RequestsPerSecond} | Concurrent: {settings.ConcurrentRequests}");
Console.WriteLine($"Ramp-up: {settings.RampUpSeconds}s");
Console.WriteLine();

// Create HTTP client with appropriate timeout
using var client = new HttpClient
{
    BaseAddress = new Uri(settings.BaseUrl),
    Timeout = TimeSpan.FromSeconds(30)
};
client.DefaultRequestHeaders.Add("X-API-Key", settings.ApiKey);

// Wait for API to be ready
Console.WriteLine("Waiting for API health check...");
await WaitForHealthyAsync(client);
Console.WriteLine("API is healthy!");

// Seed test data
Console.WriteLine("Seeding test data...");
var (testPatientId, testPrescriptionId, testOrderId) = await SeedTestDataAsync(client);
Console.WriteLine($"Test Patient: {testPatientId}");
Console.WriteLine($"Test Prescription: {testPrescriptionId}");
Console.WriteLine($"Test Order: {testOrderId}");
Console.WriteLine();

// Track total failures across all test runs
long totalFailures = 0;

switch (testType)
{
    case "load":
        totalFailures += await RunLoadTestsAsync(client, settings, testPatientId, testPrescriptionId);
        break;
    case "concurrency":
        totalFailures += await RunConcurrencyTestsAsync(client, settings, testPatientId, testPrescriptionId, testOrderId);
        break;
    case "all":
    default:
        totalFailures += await RunLoadTestsAsync(client, settings, testPatientId, testPrescriptionId);
        totalFailures += await RunConcurrencyTestsAsync(client, settings, testPatientId, testPrescriptionId, testOrderId);
        break;
}

Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                         TESTS COMPLETE                             ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

// Check for failures and exit with non-zero code if any test failed
if (totalFailures > 0)
{
    Console.WriteLine();
    Console.WriteLine($"[FAIL] Total failures: {totalFailures}");
    Environment.Exit(1);
}

// ═══════════════════════════════════════════════════════════════════════════════════════
// Helper Methods
// ═══════════════════════════════════════════════════════════════════════════════════════

async Task WaitForHealthyAsync(HttpClient httpClient)
{
    for (var i = 0; i < 30; i++)
    {
        try
        {
            var response = await httpClient.GetAsync("/health");
            if (response.IsSuccessStatusCode)
                return;
        }
        catch { /* API not ready */ }
        await Task.Delay(1000);
    }
    throw new TimeoutException("API did not become healthy within 30 seconds");
}

async Task<(Guid PatientId, Guid PrescriptionId, Guid OrderId)> SeedTestDataAsync(HttpClient httpClient)
{
    // Get existing patient or create one
    Guid patientId = await GetOrCreatePatientAsync(httpClient);
    if (patientId == Guid.Empty)
    {
        Console.WriteLine("  [WARN] Could not get or create patient");
    }

    // Get existing prescription or create one
    Guid prescriptionId = await GetOrCreatePrescriptionAsync(httpClient, patientId);
    if (prescriptionId == Guid.Empty)
    {
        Console.WriteLine("  [WARN] Could not get or create prescription");
    }

    // Create a test order for cache stampede tests
    Guid orderId = Guid.Empty;
    if (patientId != Guid.Empty && prescriptionId != Guid.Empty)
    {
        var orderRequest = new CreateOrderRequest(patientId, prescriptionId, "LoadTest-Seed");
        var orderResponse = await httpClient.PostAsJsonAsync("/api/v1/orders", orderRequest);
        if (orderResponse.IsSuccessStatusCode)
        {
            var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();
            orderId = order?.Id ?? Guid.Empty;
        }
    }

    return (patientId, prescriptionId, orderId);
}

async Task<Guid> GetOrCreatePatientAsync(HttpClient httpClient)
{
    // Always create a fresh patient for load tests to avoid conflicts with E2E test data
    var createRequest = new
    {
        firstName = "LoadTest",
        lastName = "Patient",
        email = $"loadtest.{Guid.NewGuid():N}@example.com",
        phone = "555-0100",
        dateOfBirth = DateTime.UtcNow.AddYears(-30).ToString("yyyy-MM-dd")
    };

    var createResponse = await httpClient.PostAsJsonAsync("/api/v1/patients", createRequest);
    if (createResponse.IsSuccessStatusCode)
    {
        var patientId = await ExtractIdFromResponseAsync(createResponse);
        if (patientId != Guid.Empty)
        {
            Console.WriteLine($"  [SEED] Created patient: {patientId}");
            return patientId;
        }
    }
    else
    {
        var error = await createResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"  [WARN] Failed to create patient: {createResponse.StatusCode} - {error}");
    }

    return Guid.Empty;
}

async Task<Guid> GetOrCreatePrescriptionAsync(HttpClient httpClient, Guid patientId)
{
    if (patientId == Guid.Empty)
        return Guid.Empty;

    // Always create a fresh prescription for load tests to ensure it belongs to our patient
    var createRequest = new
    {
        patientId = patientId,
        medicationName = "LoadTest Medication",
        dosage = "100mg",
        frequency = "Once daily",
        quantity = 30,
        refillsAllowed = 12, // Maximum allowed - needed for high volume write tests
        prescriberName = "Dr. LoadTest",
        expiryDate = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd"),
        instructions = "Take with water"
    };

    var createResponse = await httpClient.PostAsJsonAsync("/api/v1/prescriptions", createRequest);
    if (createResponse.IsSuccessStatusCode)
    {
        var prescriptionId = await ExtractIdFromResponseAsync(createResponse);
        if (prescriptionId != Guid.Empty)
        {
            Console.WriteLine($"  [SEED] Created prescription: {prescriptionId}");
            return prescriptionId;
        }
    }
    else
    {
        var error = await createResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"  [WARN] Failed to create prescription: {createResponse.StatusCode} - {error}");
    }

    return Guid.Empty;
}

async Task<Guid> ExtractIdFromResponseAsync(HttpResponseMessage response)
{
    try
    {
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        if (json.TryGetProperty("id", out var idElement))
        {
            if (idElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                Guid.TryParse(idElement.GetString(), out var id))
            {
                return id;
            }
        }
    }
    catch
    {
        // Fallback to regex if JSON parsing fails
        var content = await response.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(content, @"""id""\s*:\s*""([^""]+)""");
        if (match.Success && Guid.TryParse(match.Groups[1].Value, out var id))
        {
            return id;
        }
    }
    return Guid.Empty;
}

async Task<long> RunLoadTestsAsync(HttpClient httpClient, LoadTestSettings loadSettings, Guid patientId, Guid prescriptionId)
{
    Console.WriteLine("\n═══ RUNNING LOAD TESTS ═══\n");

    var readScenario = LoadTestScenarios.CreateReadLoadScenario(httpClient, loadSettings);
    var writeScenario = LoadTestScenarios.CreateWriteLoadScenario(httpClient, loadSettings, patientId, prescriptionId);
    var mixedScenario = LoadTestScenarios.CreateMixedWorkloadScenario(httpClient, loadSettings, patientId, prescriptionId);

    var result = NBomberRunner
        .RegisterScenarios(readScenario, writeScenario, mixedScenario)
        .Run();

    // Sum up all failures from all scenarios
    var failures = result.ScenarioStats.Sum(s => s.Fail.Request.Count);
    if (failures > 0)
    {
        Console.WriteLine($"\n[FAIL] Load tests had {failures} failed requests");
    }
    return failures;
}

async Task<long> RunConcurrencyTestsAsync(HttpClient httpClient, LoadTestSettings loadSettings, Guid patientId, Guid prescriptionId, Guid orderId)
{
    Console.WriteLine("\n═══ RUNNING CONCURRENCY TESTS ═══\n");
    long failures = 0;

    // Test 1: Concurrent writes - verify no duplicate IDs
    var (writeScenario, createdIds) = ConcurrencyTestScenarios.CreateConcurrentWritesScenario(
        httpClient, loadSettings, patientId, prescriptionId);

    var writeResult = NBomberRunner.RegisterScenarios(writeScenario).Run();
    failures += writeResult.ScenarioStats.Sum(s => s.Fail.Request.Count);

    // Verify data integrity
    var uniqueCount = createdIds.Distinct().Count();
    Console.WriteLine($"\n[DATA INTEGRITY] Created: {createdIds.Count}, Unique: {uniqueCount}");
    uniqueCount.ShouldBe(createdIds.Count, "DUPLICATE IDS DETECTED!");
    Console.WriteLine("[DATA INTEGRITY] ✅ No duplicate IDs\n");

    // Test 2: Read-after-write consistency
    var (rawScenario, consistencyResults) = ConcurrencyTestScenarios.CreateReadAfterWriteScenario(
        httpClient, loadSettings, patientId, prescriptionId);

    var rawResult = NBomberRunner.RegisterScenarios(rawScenario).Run();
    failures += rawResult.ScenarioStats.Sum(s => s.Fail.Request.Count);

    var consistentCount = consistencyResults.Count(r => r);
    var totalCount = consistencyResults.Count;
    Console.WriteLine($"\n[CONSISTENCY] Consistent: {consistentCount}/{totalCount}");
    consistentCount.ShouldBe(totalCount, "DATA INCONSISTENCY DETECTED!");
    Console.WriteLine("[CONSISTENCY] ✅ All read-after-write operations consistent\n");

    if (failures > 0)
    {
        Console.WriteLine($"\n[FAIL] Concurrency tests had {failures} failed requests");
    }
    return failures;
}

