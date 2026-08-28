namespace NotificationHub.Application.Abstractions;

/// <summary>
/// Explicit success/failure for <b>expected</b> business outcomes.
/// Unexpected failures (bugs, infra outages) remain exceptions.
/// Invariants: Success ⇔ Error is null; Failure ⇔ at least one Error.
/// </summary>
public class Result
{
    private readonly IReadOnlyList<Error> _errors;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    /// <summary>Primary error (first). Null when successful.</summary>
    public Error? Error => _errors.Count > 0 ? _errors[0] : null;

    /// <summary>All errors (validation may return several).</summary>
    public IReadOnlyList<Error> Errors => _errors;

    protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        if (isSuccess && errors.Count > 0)
            throw new InvalidOperationException("Success cannot carry errors.");
        if (!isSuccess && errors.Count == 0)
            throw new InvalidOperationException("Failure requires at least one error.");

        IsSuccess = isSuccess;
        _errors = errors;
    }

    public static Result Success() => new(true, Array.Empty<Error>());

    public static Result Failure(Error error)
        => new(false, new[] { error ?? throw new ArgumentNullException(nameof(error)) });

    public static Result Failure(IEnumerable<Error> errors)
    {
        var list = errors?.Where(e => e is not null).ToList()
                   ?? throw new ArgumentNullException(nameof(errors));
        if (list.Count == 0)
            throw new ArgumentException("At least one error is required.", nameof(errors));
        return new Result(false, list);
    }

    public static Result<T> Success<T>(T value) => Result<T>.Ok(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Fail(error);
    public static Result<T> Failure<T>(IEnumerable<Error> errors) => Result<T>.Fail(errors);

    public TResult Match<TResult>(Func<TResult> onSuccess, Func<IReadOnlyList<Error>, TResult> onFailure)
        => IsSuccess ? onSuccess() : onFailure(_errors);

    public Result Ensure(Func<bool> predicate, Error error)
        => IsFailure ? this : predicate() ? this : Failure(error);

    public Result Tap(Action action)
    {
        if (IsSuccess) action();
        return this;
    }

    public Result TapError(Action<IReadOnlyList<Error>> action)
    {
        if (IsFailure) action(_errors);
        return this;
    }
}

public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>Success value. Throws if accessed on failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed Result. Error: {Error?.Code}");

    /// <summary>Nullable access without throwing (null when failure).</summary>
    public T? ValueOrDefault => IsSuccess ? _value : default;

    private Result(T? value, bool isSuccess, IReadOnlyList<Error> errors)
        : base(isSuccess, errors)
        => _value = value;

    public static Result<T> Ok(T value) => new(value, true, Array.Empty<Error>());
    public static Result<T> Fail(Error error)
        => new(default, false, new[] { error ?? throw new ArgumentNullException(nameof(error)) });

    public static Result<T> Fail(IEnumerable<Error> errors)
    {
        var list = errors?.Where(e => e is not null).ToList()
                   ?? throw new ArgumentNullException(nameof(errors));
        if (list.Count == 0)
            throw new ArgumentException("At least one error is required.", nameof(errors));
        return new Result<T>(default, false, list);
    }

    // Backward-compatible aliases used across handlers
    public static Result<T> Success(T value) => Ok(value);
    public static Result<T> Failure(Error error) => Fail(error);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<IReadOnlyList<Error>, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(Errors);

    public Result<TOut> Map<TOut>(Func<T, TOut> map)
        => IsSuccess ? Result<TOut>.Ok(map(_value!)) : Result<TOut>.Fail(Errors);

    public async Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map)
        => IsSuccess ? Result<TOut>.Ok(await map(_value!)) : Result<TOut>.Fail(Errors);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
        => IsSuccess ? bind(_value!) : Result<TOut>.Fail(Errors);

    public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> bind)
        => IsSuccess ? await bind(_value!) : Result<TOut>.Fail(Errors);

    public Result<T> Ensure(Func<T, bool> predicate, Error error)
        => IsFailure ? this : predicate(_value!) ? this : Fail(error);

    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess) action(_value!);
        return this;
    }

    public static implicit operator Result<T>(Error error) => Fail(error);
    public static implicit operator Result<T>(T value) => Ok(value);
}

public static class ResultExtensions
{
    public static Result<T> ToResult<T>(this T value) => Result<T>.Ok(value);

    public static Result<T> ToResult<T>(this T? value, Error notFound) where T : class
        => value is null ? Result<T>.Fail(notFound) : Result<T>.Ok(value);

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> bind)
    {
        var result = await resultTask;
        return await result.BindAsync(bind);
    }

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<TOut>> map)
    {
        var result = await resultTask;
        return await result.MapAsync(map);
    }
}
