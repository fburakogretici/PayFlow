using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace PayFlow.SharedKernel.Results;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Successful result cannot have an error.");

        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Failed result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failure result.");

    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);
}

/// <summary>
/// Result tipini ErrorType'a göre uygun IResult HTTP yanıtına dönüştürür.
/// Endpoint'lerde tekrarlayan if/switch bloklarını ortadan kaldırır.
/// (Problem Details / RFC 7807 uyumlu)
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Microsoft.AspNetCore.Http.Results.Ok(result.Value);

        return result.Error.Type switch
        {
            ErrorType.NotFound    => Microsoft.AspNetCore.Http.Results.NotFound(new ProblemDetail(result.Error)),
            ErrorType.Validation  => Microsoft.AspNetCore.Http.Results.BadRequest(new ProblemDetail(result.Error)),
            ErrorType.Conflict    => Microsoft.AspNetCore.Http.Results.Conflict(new ProblemDetail(result.Error)),
            ErrorType.Unauthorized => Microsoft.AspNetCore.Http.Results.Unauthorized(),
            _                    => Microsoft.AspNetCore.Http.Results.Problem(result.Error.Message, statusCode: 500)
        };
    }

    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
            return Microsoft.AspNetCore.Http.Results.NoContent();

        return result.Error.Type switch
        {
            ErrorType.NotFound    => Microsoft.AspNetCore.Http.Results.NotFound(new ProblemDetail(result.Error)),
            ErrorType.Validation  => Microsoft.AspNetCore.Http.Results.BadRequest(new ProblemDetail(result.Error)),
            ErrorType.Conflict    => Microsoft.AspNetCore.Http.Results.Conflict(new ProblemDetail(result.Error)),
            ErrorType.Unauthorized => Microsoft.AspNetCore.Http.Results.Unauthorized(),
            _                    => Microsoft.AspNetCore.Http.Results.Problem(result.Error.Message, statusCode: 500)
        };
    }

    /// <summary>Error objesini doğrudan IResult'a çevirir.</summary>
    public static IResult ToHttpResult(this Error error)
    {
        return error.Type switch
        {
            ErrorType.NotFound    => Microsoft.AspNetCore.Http.Results.NotFound(new ProblemDetail(error)),
            ErrorType.Validation  => Microsoft.AspNetCore.Http.Results.BadRequest(new ProblemDetail(error)),
            ErrorType.Conflict    => Microsoft.AspNetCore.Http.Results.Conflict(new ProblemDetail(error)),
            ErrorType.Unauthorized => Microsoft.AspNetCore.Http.Results.Unauthorized(),
            _                    => Microsoft.AspNetCore.Http.Results.Problem(error.Message, statusCode: 500)
        };
    }
}


/// <summary>RFC 7807 Problem Details formatı</summary>
public sealed record ProblemDetail(string Code, string Message)
{
    public ProblemDetail(Error error) : this(error.Code, error.Message) { }
}
