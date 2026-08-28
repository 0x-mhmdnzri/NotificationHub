using NotificationHub.Application.Abstractions;

namespace NotificationHub.Host.Http;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess) return Results.NoContent();
        return MapError(result.Error!);
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        return MapError(result.Error!);
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess) return onSuccess(result.Value!);
        return MapError(result.Error!);
    }

    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { error = error.Code, message = error.Message }),
        ErrorType.NotFound => Results.NotFound(new { error = error.Code, message = error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error = error.Code, message = error.Message }),
        ErrorType.Forbidden => Results.Json(new { error = error.Code, message = error.Message }, statusCode: StatusCodes.Status403Forbidden),
        ErrorType.Unauthorized => Results.Unauthorized(),
        _ => Results.Json(new { error = error.Code, message = error.Message }, statusCode: StatusCodes.Status422UnprocessableEntity)
    };
}
