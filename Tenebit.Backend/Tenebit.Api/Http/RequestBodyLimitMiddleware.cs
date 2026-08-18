using Microsoft.AspNetCore.Http.Features;

namespace Tenebit.Api.Http;

public static class RequestSizeLimits
{
    public const long MaxJsonBodyBytes = 1L * 1024 * 1024;
    public const long MaxMultipartBodyBytes = 40L * 1024 * 1024;
    public const long MaxFormUrlEncodedBodyBytes = 64L * 1024;
    public const long MaxSingleEvidenceBodyBytes = 6L * 1024 * 1024;
    public const long MaxEvidenceFileBytes = 5L * 1024 * 1024;
    public const long MaxProcedureDocumentBodyBytes = 26L * 1024 * 1024;
    public const long MaxProcedureDocumentFileBytes = 25L * 1024 * 1024;
    public const long MaxEvidenceBundleFileBytes = 25L * 1024 * 1024;
    public const int MaxEvidenceBundleFiles = 25;
}

/// <summary>
/// Applies a small body ceiling to JSON requests before Minimal API model binding starts reading the
/// stream. Multipart requests keep the server-wide 40 MB ceiling and are narrowed further by their
/// individual upload endpoints.
/// </summary>
public sealed class RequestBodyLimitMiddleware
{
    private readonly RequestDelegate _next;

    public RequestBodyLimitMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var limit = IsJson(context.Request.ContentType)
            ? RequestSizeLimits.MaxJsonBodyBytes
            : IsFormUrlEncoded(context.Request.ContentType)
                ? RequestSizeLimits.MaxFormUrlEncodedBodyBytes
                : (long?)null;

        if (limit.HasValue)
        {
            if (context.Request.ContentLength is > 0 && context.Request.ContentLength > limit.Value)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Żądanie jest za duże.",
                    code = "PAYLOAD_TOO_LARGE"
                }, cancellationToken: context.RequestAborted);
                return;
            }

            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is not null && !feature.IsReadOnly)
            {
                feature.MaxRequestBodySize = limit.Value;
            }
        }

        await _next(context);
    }

    private static bool IsFormUrlEncoded(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJson(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }
}
