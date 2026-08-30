using Microsoft.AspNetCore.Mvc;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Host.Http;

/// <summary>
/// Maps application <see cref="Result"/> to HTTP (ProblemDetails at the boundary only).
/// Domain/Application never reference ASP.NET types.
/// </summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
            return Results.NoContent();
        return MapErrors(result.Errors);
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);
        return MapErrors(result.Errors);
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value);
        return MapErrors(result.Errors);
    }

    public static IResult ToCreatedResult<T>(this Result<T> result, Func<T, string> locationFactory)
    {
        if (result.IsSuccess)
            return Results.Created(locationFactory(result.Value), result.Value);
        return MapErrors(result.Errors);
    }

    private static IResult MapErrors(IReadOnlyList<Error> errors)
    {
        var primary = errors[0];
        var status = primary.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(primary.Type),
            Detail = primary.Message,
            Type = $"https://notificationhub/errors/{primary.Code}",
            Extensions =
            {
                ["code"] = primary.Code,
                ["errors"] = errors.Select(e => new
                {
                    code = e.Code,
                    message = e.Message,
                    type = e.Type.ToString(),
                    property = e.PropertyName
                }).ToArray()
            }
        };

        if (primary.Metadata is { Count: > 0 })
            problem.Extensions["metadata"] = primary.Metadata;

        return Results.Json(problem, statusCode: status, contentType: "application/problem+json");
    }

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Resource not found",
        ErrorType.Conflict => "Conflict",
        ErrorType.RateLimited => "Rate limit exceeded",
        ErrorType.BusinessRule => "Business rule violation",
        _ => "Operation failed"
    };
}
