namespace Tenebit.Application.Common;

public sealed record Error(string Code, string Message, int StatusCode)
{
    public static Error Validation(string message) => new("VALIDATION_ERROR", message, 400);
    public static Error Unauthorized(string message = "Wymagane uwierzytelnienie.") => new("UNAUTHORIZED", message, 401);
    public static Error Forbidden(string message = "Brak uprawnień do tej operacji.") => new("FORBIDDEN", message, 403);
    public static Error NotFound(string message) => new("NOT_FOUND", message, 404);
    public static Error Conflict(string message) => new("CONFLICT", message, 409);
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<T> : Result
{
    private Result(T? value, bool isSuccess, Error? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value, true, null);
    public static new Result<T> Failure(Error error) => new(default, false, error);
}
