using Application.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using Shouldly;

namespace Tests.Application.Behaviors;

/// <summary>
/// Test request record - must be public for Moq to create proxies.
/// </summary>
public record TestRequest(string Name) : IRequest<string>;

/// <summary>
/// Unit tests for ValidationBehavior.
/// </summary>
public class ValidationBehaviorTests
{

    private static RequestHandlerDelegate<string> CreateNextDelegate(string returnValue)
    {
        return (ct) => Task.FromResult(returnValue);
    }

    [Fact]
    public async Task Handle_WithNoValidators_ShouldCallNext()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<TestRequest>>();
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Test");

        // Act
        var result = await behavior.Handle(
            request,
            CreateNextDelegate("Success"),
            CancellationToken.None);

        // Assert
        result.ShouldBe("Success");
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldCallNext()
    {
        // Arrange
        var validatorMock = new Mock<IValidator<TestRequest>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var validators = new[] { validatorMock.Object };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Valid");

        // Act
        var result = await behavior.Handle(
            request,
            CreateNextDelegate("Success"),
            CancellationToken.None);

        // Assert
        result.ShouldBe("Success");
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldThrowValidationException()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required")
        };

        var validatorMock = new Mock<IValidator<TestRequest>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var validators = new[] { validatorMock.Object };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("");

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(() => behavior.Handle(
            request,
            CreateNextDelegate("Success"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithMultipleValidators_ShouldRunAllValidators()
    {
        // Arrange
        var validator1Mock = new Mock<IValidator<TestRequest>>();
        var validator2Mock = new Mock<IValidator<TestRequest>>();

        validator1Mock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        validator2Mock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var validators = new[] { validator1Mock.Object, validator2Mock.Object };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Test");

        // Act
        await behavior.Handle(
            request,
            CreateNextDelegate("Success"),
            CancellationToken.None);

        // Assert
        validator1Mock.Verify(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
        validator2Mock.Verify(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleFailures_ShouldIncludeAllErrors()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required"),
            new("Name", "Name is too short"),
            new("Email", "Email is invalid")
        };

        var validatorMock = new Mock<IValidator<TestRequest>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var validators = new[] { validatorMock.Object };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("");

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(() => behavior.Handle(
            request,
            CreateNextDelegate("Success"),
            CancellationToken.None));
        exception.Errors.Count().ShouldBe(3);
    }
}

