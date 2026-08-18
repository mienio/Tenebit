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
                return Results.Json(new ErrorResponse(error, "VALIDATION_ERROR"), statusCode: StatusCodes.Status400BadRequest);
            }
        }

        return await next(context);
    }
}
