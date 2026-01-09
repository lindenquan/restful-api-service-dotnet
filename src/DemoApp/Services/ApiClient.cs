using System.Net.Http.Json;
using System.Text.Json;
using DemoApp.Models;

namespace DemoApp.Services;

/// <summary>
/// HTTP client wrapper for calling the RESTful API with API key authentication.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettingsService _settingsService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiClient(HttpClient httpClient, ApiSettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    private string BaseUrl => _settingsService.Settings.BaseUrl.TrimEnd('/');
    private string ApiVersion => _settingsService.Settings.ApiVersion;

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string endpoint)
    {
        // Ensure settings are loaded before making any request
        await _settingsService.LoadSettingsAsync();

        var apiKey = _settingsService.Settings.ApiKey;
        Console.WriteLine($"[ApiClient] Creating request to {BaseUrl}{endpoint}");
        Console.WriteLine($"[ApiClient] API Key present: {!string.IsNullOrWhiteSpace(apiKey)}, Length: {apiKey?.Length ?? 0}");

        var request = new HttpRequestMessage(method, $"{BaseUrl}{endpoint}");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Add("X-Api-Key", apiKey);
            Console.WriteLine($"[ApiClient] Added X-Api-Key header");
        }
        else
        {
            Console.WriteLine($"[ApiClient] WARNING: No API key to add!");
        }

        return request;
    }

    // Health Check
    public async Task<ApiResult<HealthCheckResponse>> GetHealthAsync()
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, "/health");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<HealthCheckResponse>(JsonOptions);
                return ApiResult<HealthCheckResponse>.Success(result!);
            }

            return ApiResult<HealthCheckResponse>.Failure($"Health check failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ApiResult<HealthCheckResponse>.Failure($"Connection error: {ex.Message}");
        }
    }

    // Patients
    public async Task<ApiResult<List<PatientDto>>> GetPatientsAsync()
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/api/v{ApiVersion}/patients");
            var response = await _httpClient.SendAsync(request);
            // API returns OData format with "value" array
            var pagedResult = await HandlePagedResponse<PatientDto>(response);
            if (pagedResult.IsSuccess)
            {
                return ApiResult<List<PatientDto>>.Success(pagedResult.Data!.Value);
            }
            return ApiResult<List<PatientDto>>.Failure(pagedResult.ErrorMessage ?? "Unknown error");
        }
        catch (Exception ex)
        {
            return ApiResult<List<PatientDto>>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<ApiResult<PatientDto>> GetPatientAsync(Guid id)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/api/v{ApiVersion}/patients/{id}");
            var response = await _httpClient.SendAsync(request);
            return await HandleResponse<PatientDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<PatientDto>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<ApiResult<PatientDto>> CreatePatientAsync(CreatePatientRequest patient)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Post, $"/api/v{ApiVersion}/patients");
            request.Content = JsonContent.Create(patient);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponse<PatientDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<PatientDto>.Failure($"Error: {ex.Message}");
        }
    }

    // Prescriptions
    public async Task<ApiResult<List<PrescriptionDto>>> GetPrescriptionsAsync()
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/api/v{ApiVersion}/prescriptions");
            var response = await _httpClient.SendAsync(request);
            // API returns OData format with "value" array
            var pagedResult = await HandlePagedResponse<PrescriptionDto>(response);
            if (pagedResult.IsSuccess)
            {
                return ApiResult<List<PrescriptionDto>>.Success(pagedResult.Data!.Value);
            }
            return ApiResult<List<PrescriptionDto>>.Failure(pagedResult.ErrorMessage ?? "Unknown error");
        }
        catch (Exception ex)
        {
            return ApiResult<List<PrescriptionDto>>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<ApiResult<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionRequest prescription)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Post, $"/api/v{ApiVersion}/prescriptions");
            request.Content = JsonContent.Create(prescription);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponse<PrescriptionDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<PrescriptionDto>.Failure($"Error: {ex.Message}");
        }
    }

    // Orders
    public async Task<ApiResult<PagedResult<OrderDto>>> GetOrdersAsync(int top = 50, int skip = 0)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/api/v{ApiVersion}/orders?$top={top}&$skip={skip}&$count=true");
            var response = await _httpClient.SendAsync(request);
            return await HandlePagedResponse<OrderDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<PagedResult<OrderDto>>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<ApiResult<OrderDto>> GetOrderAsync(Guid id)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/api/v{ApiVersion}/orders/{id}");
            var response = await _httpClient.SendAsync(request);
            return await HandleResponse<OrderDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<OrderDto>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<ApiResult<OrderDto>> CreateOrderAsync(CreateOrderRequest order)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Post, $"/api/v{ApiVersion}/orders");
            request.Content = JsonContent.Create(order);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponse<OrderDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<OrderDto>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<ApiResult<OrderDto>> UpdateOrderAsync(Guid id, UpdateOrderRequest update)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Put, $"/api/v{ApiVersion}/orders/{id}");
            request.Content = JsonContent.Create(update);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponse<OrderDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<OrderDto>.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<ApiResult<bool>> DeleteOrderAsync(Guid id)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Delete, $"/api/v{ApiVersion}/orders/{id}");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return ApiResult<bool>.Success(true);
            }

            var error = await response.Content.ReadAsStringAsync();
            return ApiResult<bool>.Failure($"{response.StatusCode}: {error}");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Failure($"Error: {ex.Message}");
        }
    }

    private static async Task<ApiResult<T>> HandleResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            return ApiResult<T>.Success(result!);
        }

        var error = await response.Content.ReadAsStringAsync();
        return ApiResult<T>.Failure($"{response.StatusCode}: {error}");
    }

    private static async Task<ApiResult<PagedResult<T>>> HandlePagedResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new PagedResult<T>
            {
                Value = root.TryGetProperty("value", out var valEl)
                    ? JsonSerializer.Deserialize<List<T>>(valEl.GetRawText(), JsonOptions) ?? []
                    : [],
                ODataCount = root.TryGetProperty("@odata.count", out var countEl)
                    ? countEl.GetInt32()
                    : null,
                ODataNextLink = root.TryGetProperty("@odata.nextLink", out var nextEl)
                    ? nextEl.GetString()
                    : null
            };

            return ApiResult<PagedResult<T>>.Success(result);
        }

        var error = await response.Content.ReadAsStringAsync();
        return ApiResult<PagedResult<T>>.Failure($"{response.StatusCode}: {error}");
    }
}

