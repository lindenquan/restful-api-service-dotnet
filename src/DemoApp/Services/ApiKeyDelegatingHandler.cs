using System.Net.Http.Headers;

namespace DemoApp.Services;

/// <summary>
/// Delegating handler that adds the API key header to outgoing requests.
/// This is the recommended pattern for adding auth headers in Blazor WASM.
/// </summary>
public class ApiKeyDelegatingHandler : DelegatingHandler
{
    private readonly ApiSettingsService _settingsService;

    public ApiKeyDelegatingHandler(ApiSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _settingsService.LoadSettingsAsync();

        var apiKey = _settingsService.Settings.ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Remove existing header if present, then add
            request.Headers.Remove("X-Api-Key");
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
            Console.WriteLine($"[Handler] Added X-Api-Key header to {request.RequestUri}");
        }
        else
        {
            Console.WriteLine($"[Handler] No API key available for {request.RequestUri}");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

