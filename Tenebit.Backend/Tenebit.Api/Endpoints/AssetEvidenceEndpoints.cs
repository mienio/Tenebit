using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Alerts;
using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Audit;
using Tenebit.Domain.Alerts;
using Tenebit.Application.Audits;
using Tenebit.Domain.Audits;
using Tenebit.Application.Dashboard;
using Tenebit.Application.Evidence;
using Tenebit.Application.Identity;
using Tenebit.Application.JobProfiles;
using Tenebit.Application.Licenses;
using Tenebit.Application.Offboarding;
using Tenebit.Application.Onboarding;
using Tenebit.Application.Organizations;
using Tenebit.Application.People;
using Tenebit.Application.Procedures;
using Tenebit.Application.Reservations;
using Tenebit.Application.Settings;
using Tenebit.Application.Subscriptions;
using Tenebit.Application.Workspace;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Reservations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Api.Endpoints;

public static class AssetEvidenceEndpoints
{
    public static RouteGroupBuilder MapAssetEvidenceEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/assets/{assetId:guid}/evidence", async (Guid assetId, AssetEvidenceService service, CancellationToken cancellationToken) =>
                (await service.ListByAssetAsync(assetId, cancellationToken)).ToHttpResult())
            .WithTags("Asset evidence");

        api.MapPost("/assets/{assetId:guid}/evidence", async (Guid assetId, HttpRequest request, AssetEvidenceService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = ResultExtensions.Localize("Wyślij plik jako multipart/form-data."), code = "VALIDATION_ERROR" });
            }

            MultipartRequestHelpers.LimitRequestBody(request, MultipartRequestHelpers.MaxSingleEvidenceUploadBytes);
            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = ResultExtensions.Localize("Wybierz zdjęcie."), code = "VALIDATION_ERROR" });
            }

            if (!Enum.TryParse<EvidencePhase>(form["phase"], true, out var phase) || !Enum.IsDefined(typeof(EvidencePhase), phase))
            {
                return Results.BadRequest(new { message = ResultExtensions.Localize("Nieprawidłowy etap materiału dowodowego."), code = "VALIDATION_ERROR" });
            }

            var assignmentRaw = form["assignmentId"].ToString();
            Guid? assignmentId = null;
            if (!string.IsNullOrWhiteSpace(assignmentRaw))
            {
                if (!Guid.TryParse(assignmentRaw, out var aid) || aid == Guid.Empty)
                {
                    return Results.BadRequest(new { message = ResultExtensions.Localize("Nieprawidłowy identyfikator wydania."), code = "VALIDATION_ERROR" });
                }
                assignmentId = aid;
            }

            var caption = form["caption"].ToString();
            var uploadRequest = new UploadAssetEvidenceRequest(phase, assignmentId, string.IsNullOrWhiteSpace(caption) ? null : caption);
            var validationError = RequestObjectValidator.Validate(uploadRequest);
            if (validationError is not null)
            {
                return Results.BadRequest(new { message = ResultExtensions.Localize(validationError), code = "VALIDATION_ERROR" });
            }

            var content = await MultipartRequestHelpers.ReadFileAsync(file, RequestSizeLimits.MaxEvidenceFileBytes, cancellationToken);
            return (await service.UploadAsync(assetId, uploadRequest, file.FileName, file.ContentType, content, cancellationToken)).ToHttpResult();
        })
            .DisableAntiforgery()
            .WithTags("Asset evidence");

        api.MapGet("/evidence/{id:guid}", async (Guid id, AssetEvidenceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(id, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                return result.ToHttpResult();
            }

            var evidence = result.Value;
            return Results.File(evidence.Content, evidence.ContentType, evidence.FileName);
        })
            .WithTags("Asset evidence");

        api.MapPost("/evidence/{id:guid}/lock", async (Guid id, AssetEvidenceService service, CancellationToken cancellationToken) =>
                (await service.LockAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset evidence");

        api.MapPut("/evidence/{id:guid}/legal-hold", async (Guid id, SetEvidenceLegalHoldRequest request, AssetEvidenceService service, CancellationToken cancellationToken) =>
                (await service.SetLegalHoldAsync(id, request.Enabled, cancellationToken)).ToHttpResult())
            .WithTags("Asset evidence");

        api.MapDelete("/evidence/{id:guid}", async (Guid id, AssetEvidenceService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Asset evidence");

        return api;
    }
}
