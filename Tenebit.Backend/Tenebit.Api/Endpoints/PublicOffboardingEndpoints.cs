using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Offboarding;

namespace Tenebit.Api.Endpoints;

public static class PublicOffboardingEndpoints
{
    public static RouteGroupBuilder MapPublicOffboardingEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/public/offboarding", async (HttpRequest request, OffboardingService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.OffboardingPurpose, clock.UtcNow);
            return token is null ? Results.NotFound() : (await service.GetPublicAsync(token, ct)).ToHttpResult();
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public offboarding");

        api.MapPost("/public/offboarding/response", async (SubmitPublicOffboardingResponseRequest body, HttpRequest request, OffboardingService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.OffboardingPurpose, clock.UtcNow);
            return token is null ? Results.NotFound() : (await service.RecordEmployeeResponsesAsync(token, body, ct)).ToHttpResult();
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public offboarding");

        api.MapPost("/public/offboarding/items/{itemId:guid}/evidence", async (Guid itemId, HttpRequest request, OffboardingService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.OffboardingPurpose, clock.UtcNow);
            if (token is null) return Results.NotFound();
            if (!request.HasFormContentType) return Results.BadRequest(new { message = "Wyślij plik jako multipart/form-data.", code = "VALIDATION_ERROR" });
            MultipartRequestHelpers.LimitRequestBody(request, MultipartRequestHelpers.MaxSingleEvidenceUploadBytes);
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0) return Results.BadRequest(new { message = "Wybierz zdjęcie.", code = "VALIDATION_ERROR" });
            var content = await MultipartRequestHelpers.ReadFileAsync(file, RequestSizeLimits.MaxEvidenceFileBytes, ct);
            return (await service.UploadPublicEvidenceAsync(token, itemId, file.FileName, file.ContentType, content, ct)).ToHttpResult();
        }).DisableAntiforgery().AllowAnonymous().RequireRateLimiting("public").WithTags("Public offboarding");

        return api;
    }
}
