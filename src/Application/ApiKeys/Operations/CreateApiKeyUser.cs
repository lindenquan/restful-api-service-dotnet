using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Entities;
using FluentValidation;
using MediatR;

namespace Application.ApiKeys.Operations;

/// <summary>
/// Command to create a new API key user.
/// </summary>
public record CreateApiKeyUserCommand(
    string UserName,
    string Email,
    UserType UserType,
    string? Description = null,
    string? CreatedBy = null) : IRequest<CreateApiKeyUserResult>;

/// <summary>
/// Result of creating an API key user.
/// Contains the plain-text API key (shown only once).
/// </summary>
public record CreateApiKeyUserResult(
    int UserId,
    string ApiKey,        // Plain-text key - SHOW ONLY ONCE
    string ApiKeyPrefix,  // First 8 chars for identification
    string UserName,
    string Email,
    UserType UserType);

/// <summary>
/// Validator for CreateApiKeyUserCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreateApiKeyUserCommandValidator : AbstractValidator<CreateApiKeyUserCommand>
{
    public CreateApiKeyUserCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("User name is required")
            .MaximumLength(100).WithMessage("User name must not exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(200).WithMessage("Email must not exceed 200 characters");

        RuleFor(x => x.UserType)
            .IsInEnum().WithMessage("Invalid user type");
    }
}

/// <summary>
/// Handler for CreateApiKeyUserCommand.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class CreateApiKeyUserHandler : IRequestHandler<CreateApiKeyUserCommand, CreateApiKeyUserResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApiKeyGenerator _apiKeyGenerator;

    public CreateApiKeyUserHandler(IUnitOfWork unitOfWork, IApiKeyGenerator apiKeyGenerator)
    {
        _unitOfWork = unitOfWork;
        _apiKeyGenerator = apiKeyGenerator;
    }

    public async Task<CreateApiKeyUserResult> Handle(CreateApiKeyUserCommand request, CancellationToken cancellationToken)
    {
        // Check if email already exists
        var existingUser = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser != null)
        {
            throw new InvalidOperationException($"User with email '{request.Email}' already exists.");
        }

        // Generate new API key
        var plainTextApiKey = _apiKeyGenerator.GenerateApiKey();
        var apiKeyHash = _apiKeyGenerator.HashApiKey(plainTextApiKey);
        var apiKeyPrefix = _apiKeyGenerator.GetKeyPrefix(plainTextApiKey);

        // Create the user entity
        var user = new User
        {
            ApiKeyHash = apiKeyHash,
            ApiKeyPrefix = apiKeyPrefix,
            UserName = request.UserName,
            Email = request.Email,
            UserType = request.UserType,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateApiKeyUserResult(
            UserId: user.Id,
            ApiKey: plainTextApiKey,  // Return plain-text key ONLY HERE
            ApiKeyPrefix: apiKeyPrefix,
            UserName: user.UserName,
            Email: user.Email,
            UserType: user.UserType);
    }
}

