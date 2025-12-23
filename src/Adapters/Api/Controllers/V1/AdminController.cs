using Adapters.Api.Authorization;
using Adapters.Api.Services;
using Application.ApiKeys.Operations;
using Asp.Versioning;
using Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adapters.Api.Controllers.V1;

/// <summary>
/// Admin-only endpoints for managing API keys and users.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = PolicyNames.AdminOnly)]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public AdminController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Create a new API key user.
    /// The plain-text API key is returned ONLY in this response - store it securely!
    /// </summary>
    [HttpPost("api-keys")]
    [ProducesResponseType(typeof(CreateApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateApiKeyResponse>> CreateApiKey(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken ct)
    {
        var command = new CreateApiKeyUserCommand(
            UserName: request.UserName,
            Email: request.Email,
            UserType: request.UserType,
            Description: request.Description,
            CreatedBy: _currentUser.UserName ?? _currentUser.UserId?.ToString());

        try
        {
            var result = await _mediator.Send(command, ct);

            var response = new CreateApiKeyResponse(
                UserId: result.UserId,
                ApiKey: result.ApiKey,
                ApiKeyPrefix: result.ApiKeyPrefix,
                UserName: result.UserName,
                Email: result.Email,
                UserType: result.UserType.ToString(),
                Message: "Store this API key securely - it will NOT be shown again!");

            return CreatedAtAction(nameof(CreateApiKey), new { id = result.UserId }, response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request to create a new API key user.
/// </summary>
public record CreateApiKeyRequest(
    string UserName,
    string Email,
    UserType UserType,
    string? Description = null);

/// <summary>
/// Response containing the newly created API key.
/// WARNING: The ApiKey is shown ONLY in this response!
/// </summary>
public record CreateApiKeyResponse(
    int UserId,
    string ApiKey,
    string ApiKeyPrefix,
    string UserName,
    string Email,
    string UserType,
    string Message);

