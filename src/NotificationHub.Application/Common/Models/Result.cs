namespace NotificationHub.Application.Common.Models;

public sealed class Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public int? StatusCode { get; init; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error, string? code = null, int? status = null)
        => new() { IsSuccess = false, Error = error, ErrorCode = code, StatusCode = status };
}
