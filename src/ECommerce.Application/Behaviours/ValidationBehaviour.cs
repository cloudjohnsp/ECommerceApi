using ECommerce.Shared.Results;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerce.Application.Behaviors
{
    public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : class, IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators = validators;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!_validators.Any())
            {
                return await next(cancellationToken);
            }

            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

            if (failures.Count > 0)
            {
                if (typeof(TResponse).IsGenericType &&
                    typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                {
                    var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));
                    var resultType = typeof(TResponse).GetGenericArguments()[0];

                    var genericResultType = typeof(Result<>).MakeGenericType(resultType);
                    var failMethod = genericResultType.GetMethod(
                        "Failure",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null,
                        [typeof(string[])],
                        null) ?? throw new InvalidOperationException($"Não foi possível localizar Result<{resultType.Name}>.Failure(params string[])");

                    var invoked = failMethod.Invoke(null, [new[] { errorMessage }]);
                    return (TResponse)invoked!;
                }

                throw new ValidationException(failures);
            }

            return await next(cancellationToken);
        }
    }
}
