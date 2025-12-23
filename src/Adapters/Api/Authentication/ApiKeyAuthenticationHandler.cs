using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Adapters.Api.Authentication;

/// <summary>
/// API Key authentication scheme name.
/// </summary>
public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

/// <summary>
/// Options for API Key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// Authentication handler that validates API keys from the X-Api-Key header.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IServiceProvider _serviceProvider;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceProvider serviceProvider)
        : base(options, logger, encoder)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if the API key header is present
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.Fail("API Key header not found.");
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.Fail("API Key is empty.");
        }

        // Get the unit of work from scoped services
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Validate API key and get user
        var user = await unitOfWork.Users.GetByApiKeyAsync(providedApiKey);

        if (user == null)
        {
            return AuthenticateResult.Fail("Invalid API Key.");
        }

        if (!user.IsActive)
        {
            return AuthenticateResult.Fail("API Key is inactive.");
        }

        // Update last used timestamp (pass the hash, not the plain key)
        await unitOfWork.Users.UpdateLastUsedAsync(user.ApiKeyHash);
        await unitOfWork.SaveChangesAsync();

        // Create claims for the authenticated user
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.UserType.ToString()),
            new Claim("ApiKeyId", user.Id.ToString()),
            new Claim("UserType", user.UserType.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

