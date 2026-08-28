using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Audits;
using Tenebit.Application.Common;

namespace Tenebit.Api.Endpoints;

public static class PublicAssetAuditsEndpoints
{
    public static RouteGroupBuilder MapPublicAssetAuditsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/public/asset-audits", async (HttpRequest request, AssetAuditCampaignService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssetAuditPurpose, clock.UtcNow);
            return token is null ? Results.NotFound() : (await service.GetPublicAsync(token, ct)).ToHttpResult();
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public asset audits");

        api.MapPut("/public/asset-audits/items/{itemId:guid}", async (Guid itemId, SubmitPublicAssetAuditItemRequest body, HttpRequest request, AssetAuditCampaignService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssetAuditPurpose, clock.UtcNow);
            return token is null ? Results.NotFound() : (await service.RecordItemResponseAsync(token, itemId, body, ct)).ToHttpResult();
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public asset audits");

        api.MapPost("/public/asset-audits/submit", async (HttpRequest request, AssetAuditCampaignService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssetAuditPurpose, clock.UtcNow);
            return token is null ? Results.NotFound() : (await service.SubmitAsync(token, ct)).ToHttpResult();
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public asset audits");

        api.MapPost("/public/asset-audits/items/{itemId:guid}/evidence", async (Guid itemId, HttpRequest request, AssetAuditCampaignService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssetAuditPurpose, clock.UtcNow);
            if (token is null) return Results.NotFound();
            if (!request.HasFormContentType) return Results.BadRequest(new { message = ResultExtensions.Localize("Wyślij plik jako multipart/form-data."), code = "VALIDATION_ERROR" });
            MultipartRequestHelpers.LimitRequestBody(request, MultipartRequestHelpers.MaxSingleEvidenceUploadBytes);
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0) return Results.BadRequest(new { message = ResultExtensions.Localize("Wybierz zdjęcie."), code = "VALIDATION_ERROR" });
            var content = await MultipartRequestHelpers.ReadFileAsync(file, RequestSizeLimits.MaxEvidenceFileBytes, ct);
            return (await service.UploadPublicEvidenceAsync(token, itemId, file.FileName, file.ContentType, content, ct)).ToHttpResult();
        }).DisableAntiforgery().AllowAnonymous().RequireRateLimiting("public").WithTags("Public asset audits");

        return api;
    }
}
