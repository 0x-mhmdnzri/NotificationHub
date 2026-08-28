using FluentValidation;
using MediatR;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Behaviors;

/// <summary>
/// Validation failures are expected outcomes → Result when TResponse is Result/Result&lt;T&gt;.
/// Otherwise throw ValidationException (non-Result handlers).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var errors = failures
            .Select(f => Error.Validation(
                code: string.IsNullOrWhiteSpace(f.ErrorCode) || f.ErrorCode == "NotEmptyValidator"
                    ? $"validation.{f.PropertyName}".ToLowerInvariant()
                    : f.ErrorCode,
                message: f.ErrorMessage,
                property: f.PropertyName))
            .ToList();

        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(errors);

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = typeof(TResponse);
            var failMethod = resultType.GetMethod(
                nameof(Result<object>.Fail),
                [typeof(IEnumerable<Error>)]);
            if (failMethod is not null)
            {
                var failed = failMethod.Invoke(null, [errors]);
                return (TResponse)failed!;
            }
        }

        throw new ValidationException(failures);
    }
}
