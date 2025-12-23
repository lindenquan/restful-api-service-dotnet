using FluentValidation;
using MediatR;

namespace Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for request validation.
/// Runs all registered validators before the handler executes.
/// Throws ValidationException if any validation fails.
/// Sealed for performance optimization and design intent.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // If no validators registered for this request type, continue
        if (!_validators.Any())
        {
            return await next();
        }

        // Create validation context
        var context = new ValidationContext<TRequest>(request);

        // Run all validators in parallel
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Collect all failures
        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure != null)
            .ToList();

        // If there are failures, throw ValidationException
        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}

/// <summary>
/// Custom exception for validation errors with structured error details.
/// </summary>
public class RequestValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public RequestValidationException(IEnumerable<FluentValidation.Results.ValidationFailure> failures)
        : base("One or more validation failures have occurred.")
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}

