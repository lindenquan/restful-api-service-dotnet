using System.Text.Json;
using DemoApp.Models;
using Microsoft.JSInterop;

namespace DemoApp.Services;

/// <summary>
/// Service for managing API settings (base URL, API key) with local storage persistence.
/// </summary>
public class ApiSettingsService
{
    private readonly IJSRuntime _jsRuntime;
    private ApiSettings _settings = new();
    private bool _isLoaded;

    private const string StorageKey = "demo-app-api-settings";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public event Action? OnSettingsChanged;

    public ApiSettingsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ApiSettings Settings => _settings;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public async Task LoadSettingsAsync()
    {
        if (_isLoaded)
        {
            Console.WriteLine($"[Settings] Already loaded. ApiKey length: {_settings.ApiKey?.Length ?? 0}");
            return;
        }

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            Console.WriteLine($"[Settings] Loaded from localStorage: {json?.Substring(0, Math.Min(json?.Length ?? 0, 100))}...");
            if (!string.IsNullOrEmpty(json))
            {
                var settings = JsonSerializer.Deserialize<ApiSettings>(json, JsonOptions);
                if (settings != null)
                {
                    _settings = settings;
                    Console.WriteLine($"[Settings] Deserialized. ApiKey length: {_settings.ApiKey?.Length ?? 0}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Error loading: {ex.Message}");
        }

        _isLoaded = true;
    }

    public async Task SaveSettingsAsync(ApiSettings settings)
    {
        _settings = settings;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(settings);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // Ignore errors
        }

        OnSettingsChanged?.Invoke();
    }

    public async Task ClearSettingsAsync()
    {
        _settings = new ApiSettings();

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch
        {
            // Ignore errors
        }

        OnSettingsChanged?.Invoke();
    }
}

