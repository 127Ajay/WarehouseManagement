using System.Diagnostics.CodeAnalysis;

namespace Wms.SharedKernel;

/// <summary>Marker for "no value" results.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value;
}

/// <summary>
/// Outcome of an operation. Business rule failures are returned, never thrown.
/// Exceptions are reserved for bugs and infrastructure faults.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        Error = null;
    }

    private Result(Error error)
    {
        _value = default;
        Error = error;
    }

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    /// <summary>The value. Throws if the result is a failure, so check <see cref="IsSuccess"/> first.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value of a failed result ({Error.Code}).");

    public static Result<T> Ok(T value) => new(value);

    public static Result<T> Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(Error error) => Fail(error);

    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Result<TOut>.Ok(map(_value!)) : Result<TOut>.Fail(Error);
    }

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(Error);
    }
}

/// <summary>Helpers for results without a value.</summary>
public static class Result
{
    public static Result<Unit> Ok() => Result<Unit>.Ok(Unit.Value);

    public static Result<Unit> Fail(Error error) => Result<Unit>.Fail(error);
}
