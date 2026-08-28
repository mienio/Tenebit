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

    public static IResult? ToHttpResultIfFailure(this Result result) =>
        result.IsSuccess ? null : ToErrorResult(result.Error!);

    /// <summary>
    /// Tłumaczy komunikat napisany po polsku w warstwie API na język bieżącego żądania.
    ///
    /// Wyniki serwisów trafiają tu same, przez <see cref="ToErrorResult"/>. Ale część endpointów
    /// odpowiada z pominięciem <c>Result</c> - limity prób logowania, wygasła sesja, walidacja
    /// multipart - i taki komunikat nie przechodził przez żaden translator, więc obcojęzyczny
    /// użytkownik dostawał polski tekst na ekranie logowania. Te miejsca muszą wołać to jawnie.
    /// </summary>
    public static string Localize(string message) =>
        ErrorMessageTranslator.Translate(message, RequestLanguageAccessor.CurrentLanguage);

    private static IResult ToErrorResult(Error error)
    {
        var message = ErrorMessageTranslator.Translate(error.Message, RequestLanguageAccessor.CurrentLanguage);
        return Results.Json(new ErrorResponse(message, error.Code), statusCode: error.StatusCode);
    }
}
