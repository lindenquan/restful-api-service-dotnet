using Application.Users.Operations;
using Domain;
using FluentValidation.TestHelper;

namespace Tests.Application.Users;

/// <summary>
/// Unit tests for CreateApiKeyUserCommandValidator.
/// </summary>
public class CreateApiKeyUserValidatorTests
{
    private readonly CreateApiKeyUserCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: "valid-user",
            Email: "valid@example.com",
            UserType: UserType.Regular);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyUserName_ShouldHaveError(string? userName)
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: userName!,
            Email: "valid@example.com",
            UserType: UserType.Regular);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserName)
            .WithErrorMessage("User name is required");
    }

    [Fact]
    public void Validate_UserNameTooLong_ShouldHaveError()
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: new string('a', 101),
            Email: "valid@example.com",
            UserType: UserType.Regular);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserName)
            .WithErrorMessage("User name must not exceed 100 characters");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyEmail_ShouldHaveError(string? email)
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: "valid-user",
            Email: email!,
            UserType: UserType.Regular);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@domain.com")]
    public void Validate_InvalidEmailFormat_ShouldHaveError(string email)
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: "valid-user",
            Email: email,
            UserType: UserType.Regular);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Invalid email format");
    }

    [Fact]
    public void Validate_EmailTooLong_ShouldHaveError()
    {
        // Arrange
        var longEmail = new string('a', 190) + "@example.com";
        var command = new CreateApiKeyUserCommand(
            UserName: "valid-user",
            Email: longEmail,
            UserType: UserType.Regular);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not exceed 200 characters");
    }

    [Fact]
    public void Validate_InvalidUserType_ShouldHaveError()
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: "valid-user",
            Email: "valid@example.com",
            UserType: (UserType)99);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserType)
            .WithErrorMessage("Invalid user type");
    }

    [Theory]
    [InlineData(UserType.Regular)]
    [InlineData(UserType.Admin)]
    public void Validate_ValidUserType_ShouldNotHaveError(UserType userType)
    {
        // Arrange
        var command = new CreateApiKeyUserCommand(
            UserName: "valid-user",
            Email: "valid@example.com",
            UserType: userType);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserType);
    }
}

