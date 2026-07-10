using Tenebit.Application.Common;

namespace Tenebit.Api.Http;

public sealed record ErrorResponse(string Message, string Code);

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToErrorResult(result.Error!);
    }

    public static IResult ToCreatedResult<T>(this Result<T> result, Func<T, string> location)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            return Results.Created(location(result.Value), result.Value);
        }

        return ToErrorResult(result.Error!);
    }

    public static IResult ToNoContentResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : ToErrorResult(result.Error!);

    private static IResult ToErrorResult(Error error) =>
        Results.Json(new ErrorResponse(error.Message, error.Code), statusCode: error.StatusCode);
}
