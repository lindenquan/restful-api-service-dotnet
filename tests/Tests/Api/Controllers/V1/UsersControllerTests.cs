using Application.Users.Operations;
using Domain;
using DTOs.Shared;
using Infrastructure.Api.Controllers.V1;
using Infrastructure.Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shouldly;

namespace Tests.Api.Controllers.V1;

/// <summary>
/// Unit tests for UsersController.
/// </summary>
public class UsersControllerTests
{
    private static readonly Guid TestUserId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _currentUserMock = new Mock<ICurrentUserService>();

        _currentUserMock.Setup(c => c.UserName).Returns("test-admin");
        _currentUserMock.Setup(c => c.UserId).Returns(TestUserId);

        _controller = new UsersController(_mediatorMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnCreatedResult()
    {
        // Arrange
        var request = new CreateUserRequest(
            UserName: "new-user",
            Email: "new@example.com",
            UserType: UserType.Regular,
            Description: "Test description");

        var commandResult = new CreateApiKeyUserResult(
            UserId: TestUserId,
            ApiKey: "generated-api-key",
            ApiKeyPrefix: "generate...",
            UserName: "new-user",
            Email: "new@example.com",
            UserType: UserType.Regular);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateApiKeyUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.ShouldBeOfType<CreatedAtActionResult>();
        var response = createdResult.Value.ShouldBeOfType<CreateUserResponse>();

        response.UserId.ShouldBe(TestUserId);
        response.ApiKey.ShouldBe("generated-api-key");
        response.UserName.ShouldBe("new-user");
        response.Email.ShouldBe("new@example.com");
        response.UserType.ShouldBe("Regular");
        response.Message.ShouldContain("Store this API key securely");
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ShouldReturnConflict()
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
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        result.Result.ShouldBeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldPassCurrentUserAsCreatedBy()
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
            .ReturnsAsync(new CreateApiKeyUserResult(TestUserId, "key", "prefix", "user", "email", UserType.Admin));

        // Act
        await _controller.Create(request, CancellationToken.None);

        // Assert
        capturedCommand.ShouldNotBeNull();
        capturedCommand!.CreatedBy.ShouldBe(TestUserId); // UserId from mock
    }

    [Fact]
    public async Task Create_AdminUser_ShouldSetCorrectUserType()
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
            .ReturnsAsync(new CreateApiKeyUserResult(TestUserId, "key", "prefix", "user", "email", UserType.Admin));

        // Act
        await _controller.Create(request, CancellationToken.None);

        // Assert
        capturedCommand.ShouldNotBeNull();
        capturedCommand!.UserType.ShouldBe(UserType.Admin);
    }
}

