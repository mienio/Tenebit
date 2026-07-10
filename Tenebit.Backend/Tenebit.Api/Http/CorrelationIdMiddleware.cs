using Serilog.Context;

namespace Tenebit.Api.Http;

public static class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var provided) && !string.IsNullOrWhiteSpace(provided)
            ? provided.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            context.Items[HeaderName] = correlationId;
            await next(context);
        }
    });
}
