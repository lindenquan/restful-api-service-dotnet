using Adapters.Api.Controllers.V1;
using Adapters.Api.Services;
using Application.ApiKeys.Operations;
using DTOs.Shared;
using Entities;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Tests.Api.Controllers;

/// <summary>
/// Unit tests for AdminController.
/// </summary>
public class AdminControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _currentUserMock = new Mock<ICurrentUserService>();

        _currentUserMock.Setup(c => c.UserName).Returns("test-admin");
        _currentUserMock.Setup(c => c.UserId).Returns(1);

        _controller = new AdminController(_mediatorMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task CreateApiKey_WithValidRequest_ShouldReturnCreatedResult()
    {
        // Arrange
        var request = new CreateUserRequest(
            UserName: "new-user",
            Email: "new@example.com",
            UserType: UserType.Regular,
            Description: "Test description");

        var commandResult = new CreateApiKeyUserResult(
            UserId: 1,
            ApiKey: "generated-api-key",
            ApiKeyPrefix: "generate...",
            UserName: "new-user",
            Email: "new@example.com",
            UserType: UserType.Regular);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateApiKeyUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _controller.CreateApiKey(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<CreateUserResponse>().Subject;

        response.UserId.Should().Be(1);
        response.ApiKey.Should().Be("generated-api-key");
        response.UserName.Should().Be("new-user");
        response.Email.Should().Be("new@example.com");
        response.UserType.Should().Be("Regular");
        response.Message.Should().Contain("Store this API key securely");
    }

    [Fact]
    public async Task CreateApiKey_WithDuplicateEmail_ShouldReturnConflict()
    {
        // Arrange
        var request = new CreateUserRequest(
            UserName: "existing-user",
            Email: "existing@example.com",
            UserType: UserType.Regular);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateApiKeyUserCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User with email 'existing@example.com' already exists."));

        // Act
        var result = await _controller.CreateApiKey(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateApiKey_ShouldPassCurrentUserAsCreatedBy()
    {
        // Arrange
        var request = new CreateUserRequest(
            UserName: "new-user",
            Email: "new@example.com",
            UserType: UserType.Admin);

        CreateApiKeyUserCommand? capturedCommand = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateApiKeyUserCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateApiKeyUserResult>, CancellationToken>((cmd, _) =>
                capturedCommand = cmd as CreateApiKeyUserCommand)
            .ReturnsAsync(new CreateApiKeyUserResult(1, "key", "prefix", "user", "email", UserType.Admin));

        // Act
        await _controller.CreateApiKey(request, CancellationToken.None);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.CreatedBy.Should().Be("test-admin");
    }

    [Fact]
    public async Task CreateApiKey_AdminUser_ShouldSetCorrectUserType()
    {
        // Arrange
        var request = new CreateUserRequest(
            UserName: "admin-user",
            Email: "admin@example.com",
            UserType: UserType.Admin);

        CreateApiKeyUserCommand? capturedCommand = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateApiKeyUserCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateApiKeyUserResult>, CancellationToken>((cmd, _) =>
                capturedCommand = cmd as CreateApiKeyUserCommand)
            .ReturnsAsync(new CreateApiKeyUserResult(1, "key", "prefix", "user", "email", UserType.Admin));

        // Act
        await _controller.CreateApiKey(request, CancellationToken.None);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.UserType.Should().Be(UserType.Admin);
    }
}

