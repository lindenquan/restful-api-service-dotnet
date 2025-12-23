using Adapters.ApiClient.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Adapters.ApiClient;

/// <summary>
/// Extension methods for registering API clients in DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all API clients (V1 and V2) with the specified base URL and API key.
    /// </summary>
    public static IServiceCollection AddPrescriptionOrderApiClients(
        this IServiceCollection services,
        string baseUrl,
        string apiKey)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(apiKey);

        // Register V1 clients
        services.AddRefitClient<V1.IOrdersApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler(() => new ApiKeyAuthenticationHandler(apiKey));

        services.AddRefitClient<V1.IAdminApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler(() => new ApiKeyAuthenticationHandler(apiKey));

        // Register V2 clients
        services.AddRefitClient<V2.IOrdersApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler(() => new ApiKeyAuthenticationHandler(apiKey));

        return services;
    }

    /// <summary>
    /// Register V1 API clients only.
    /// </summary>
    public static IServiceCollection AddPrescriptionOrderApiClientsV1(
        this IServiceCollection services,
        string baseUrl,
        string apiKey)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(apiKey);

        services.AddRefitClient<V1.IOrdersApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler(() => new ApiKeyAuthenticationHandler(apiKey));

        services.AddRefitClient<V1.IAdminApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler(() => new ApiKeyAuthenticationHandler(apiKey));

        return services;
    }

    /// <summary>
    /// Register V2 API clients only.
    /// </summary>
    public static IServiceCollection AddPrescriptionOrderApiClientsV2(
        this IServiceCollection services,
        string baseUrl,
        string apiKey)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(apiKey);

        services.AddRefitClient<V2.IOrdersApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler(() => new ApiKeyAuthenticationHandler(apiKey));

        return services;
    }
}

