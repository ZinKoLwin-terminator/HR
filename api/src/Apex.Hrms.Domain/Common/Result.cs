namespace Apex.Hrms.Domain.Common;

/// <summary>Outcome of an operation that can fail for expected, business reasons.</summary>
public class Result
{
    private readonly AppError? error;

    protected Result(AppError? error) => this.error = error;

    public bool IsSuccess => error is null;

    public bool IsFailure => error is not null;

    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public AppError Error => error ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new(null);

    public static Result Failure(AppError error) => new(error);

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(AppError error) => new(default, error);
}

/// <summary>A <see cref="Result"/> that carries a value when it succeeds.</summary>
public sealed class Result<T> : Result
{
    private readonly T? value;

    internal Result(T? value, AppError? error)
        : base(error) => this.value = value;

    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed result has no value.");
}
