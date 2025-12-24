using Adapters.Api.Authorization;
using Adapters.Api.Services;
using Application.ApiKeys.Operations;
using Asp.Versioning;
using DTOs.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adapters.Api.Controllers.V1;

/// <summary>
/// Admin-only endpoints for managing users.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize(Policy = PolicyNames.AdminOnly)]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public UsersController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Create a new user with API key.
    /// The plain-text API key is returned ONLY in this response - store it securely!
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateUserResponse>> Create(
        [FromBody] CreateUserRequest request,
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

            var response = new CreateUserResponse(
                UserId: result.UserId,
                ApiKey: result.ApiKey,
                ApiKeyPrefix: result.ApiKeyPrefix,
                UserName: result.UserName,
                Email: result.Email,
                UserType: result.UserType.ToString(),
                Message: "Store this API key securely - it will NOT be shown again!");

            return CreatedAtAction(nameof(Create), new { id = result.UserId }, response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

