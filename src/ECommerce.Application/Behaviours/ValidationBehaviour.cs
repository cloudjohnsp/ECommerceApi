using ECommerce.Shared.Results;
using FluentValidation;
using MediatR;
namespace ECommerce.Application.Behaviors;

public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(cancellationToken);

        var errors = failures
            .Select(failure => failure.ErrorMessage)
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(errors);

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = typeof(TResponse).GetGenericArguments()[0];
            var genericResultType = typeof(Result<>).MakeGenericType(resultType);
            var failureMethod = genericResultType.GetMethod(
                nameof(Result.Failure),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                [typeof(string[])],
                null) ?? throw new InvalidOperationException(
                    $"Unable to locate Result<{resultType.Name}>.Failure(params string[]).");

            return (TResponse)failureMethod.Invoke(null, [errors])!;
        }

        throw new ValidationException(failures);
    }
}
