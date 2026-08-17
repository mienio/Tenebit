namespace Tenebit.Application.Common;

public sealed record Error(string Code, string Message, int StatusCode)
{
    // Audit P2 #7: each factory resolves a stable, message-specific code (e.g. "ASSET_NOT_FOUND")
    // via ErrorCodeResolver instead of a generic per-status code, so API consumers can branch on the
    // error without matching translated text. Falls back to the old generic code for any message not
    // (yet) in the resolver's table, so unmapped call sites keep working unchanged.
    public static Error Validation(string message) => new(ErrorCodeResolver.Resolve(message) ?? "VALIDATION_ERROR", message, 400);
    public static Error Unauthorized(string message = "Wymagane uwierzytelnienie.") => new(ErrorCodeResolver.Resolve(message) ?? "UNAUTHORIZED", message, 401);
    public static Error Forbidden(string message = "Brak uprawnień do tej operacji.") => new(ErrorCodeResolver.Resolve(message) ?? "FORBIDDEN", message, 403);
    public static Error NotFound(string message) => new(ErrorCodeResolver.Resolve(message) ?? "NOT_FOUND", message, 404);
    public static Error Conflict(string message) => new(ErrorCodeResolver.Resolve(message) ?? "CONFLICT", message, 409);
    public static Error TooManyRequests(string message) => new(ErrorCodeResolver.Resolve(message) ?? "TOO_MANY_REQUESTS", message, 429);
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

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
