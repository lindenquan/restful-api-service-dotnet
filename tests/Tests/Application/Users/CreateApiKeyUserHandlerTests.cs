using Application.Users.Operations;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain;
using FluentAssertions;
using Moq;

namespace Tests.Application.Users;

/// <summary>
/// Unit tests for CreateApiKeyUserHandler.
/// </summary>
public class CreateApiKeyUserHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IApiKeyGenerator> _apiKeyGeneratorMock;
    private readonly CreateApiKeyUserHandler _handler;

    public CreateApiKeyUserHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();
        _apiKeyGeneratorMock = new Mock<IApiKeyGenerator>();

        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        _handler = new CreateApiKeyUserHandler(_unitOfWorkMock.Object, _apiKeyGeneratorMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateUserAndReturnResultAsync()
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: "test-user",
            Email: "test@example.com",
            UserType: UserType.Regular,
            Description: "Test user",
            CreatedBy: "admin");

        _userRepoMock
            .Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _apiKeyGeneratorMock.Setup(g => g.GenerateApiKey()).Returns("generated-api-key");
        _apiKeyGeneratorMock.Setup(g => g.HashApiKey("generated-api-key")).Returns("hashed-key");
        _apiKeyGeneratorMock.Setup(g => g.GetKeyPrefix("generated-api-key")).Returns("generate...");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ApiKey.Should().Be("generated-api-key");
        result.ApiKeyPrefix.Should().Be("generate...");
        result.UserName.Should().Be("test-user");
        result.Email.Should().Be("test@example.com");
        result.UserType.Should().Be(UserType.Regular);

        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldThrowInvalidOperationExceptionAsync()
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: "test-user",
            Email: "existing@example.com",
            UserType: UserType.Regular);

        var existingUser = new User { Id = 1, Email = "existing@example.com" };
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_AdminUser_ShouldSetCorrectUserTypeAsync()
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: "admin-user",
            Email: "admin@example.com",
            UserType: UserType.Admin);

        _userRepoMock
            .Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _apiKeyGeneratorMock.Setup(g => g.GenerateApiKey()).Returns("admin-key");
        _apiKeyGeneratorMock.Setup(g => g.HashApiKey(It.IsAny<string>())).Returns("hashed");
        _apiKeyGeneratorMock.Setup(g => g.GetKeyPrefix(It.IsAny<string>())).Returns("prefix");

        User? capturedUser = null;
        _userRepoMock
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => capturedUser = user)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.UserType.Should().Be(UserType.Admin);
        capturedUser.Should().NotBeNull();
        capturedUser!.UserType.Should().Be(UserType.Admin);
        capturedUser.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldGenerateUniqueApiKeyForEachUserAsync()
    {
        // Arrange
        var callCount = 0;
        _apiKeyGeneratorMock.Setup(g => g.GenerateApiKey())
            .Returns(() => $"key-{++callCount}");
        _apiKeyGeneratorMock.Setup(g => g.HashApiKey(It.IsAny<string>())).Returns("hash");
        _apiKeyGeneratorMock.Setup(g => g.GetKeyPrefix(It.IsAny<string>())).Returns("prefix");

        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command1 = new CreateApiKeyUserCommand("user1", "user1@test.com", UserType.Regular);
        var command2 = new CreateApiKeyUserCommand("user2", "user2@test.com", UserType.Regular);

        // Act
        var result1 = await _handler.Handle(command1, CancellationToken.None);
        var result2 = await _handler.Handle(command2, CancellationToken.None);

        // Assert
        result1.ApiKey.Should().Be("key-1");
        result2.ApiKey.Should().Be("key-2");
    }
}

