using Tenebit.Application.Common;

namespace Tenebit.Api.Http;

/// <summary>Runs the shared request-contract validator before every Minimal API handler.</summary>
public sealed class ValidationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            var error = RequestObjectValidator.Validate(argument);
            if (error is not null)
            {
                // Walidator pisze po polsku (jak cala warstwa aplikacji), a ten filtr odpowiada
                // z pominieciem Result, wiec tlumaczenie musi byc tutaj - inaczej kazdy blad
                // walidacji wracal po polsku niezaleznie od jezyka interfejsu.
                return Results.Json(new ErrorResponse(ResultExtensions.Localize(error), "VALIDATION_ERROR"), statusCode: StatusCodes.Status400BadRequest);
            }
        }

        return await next(context);
    }
}
