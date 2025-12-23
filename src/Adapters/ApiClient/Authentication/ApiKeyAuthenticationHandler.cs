namespace Adapters.ApiClient.Authentication;

/// <summary>
/// HTTP message handler that adds API key to all outgoing requests.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : DelegatingHandler
{
    private readonly string _apiKey;

    public ApiKeyAuthenticationHandler(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add API key header to every request
        request.Headers.Add("X-API-Key", _apiKey);

        return base.SendAsync(request, cancellationToken);
    }
}

