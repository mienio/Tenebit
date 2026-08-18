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

public static class PublicOffboardingEndpoints
{
    public static RouteGroupBuilder MapPublicOffboardingEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/public/offboarding/{token}", async (string token, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.GetPublicAsync(token, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public offboarding");

        api.MapPost("/public/offboarding/{token}/response", async (string token, SubmitPublicOffboardingResponseRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.RecordEmployeeResponsesAsync(token, request, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public offboarding");

        api.MapPost("/public/offboarding/{token}/items/{itemId:guid}/evidence", async (string token, Guid itemId, HttpRequest request, OffboardingService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Wyślij plik jako multipart/form-data.", code = "VALIDATION_ERROR" });
            }

            MultipartRequestHelpers.LimitRequestBody(request, MultipartRequestHelpers.MaxSingleEvidenceUploadBytes);
            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "Wybierz zdjęcie.", code = "VALIDATION_ERROR" });
            }

            var content = await MultipartRequestHelpers.ReadFileAsync(file, RequestSizeLimits.MaxEvidenceFileBytes, cancellationToken);
            var result = await service.UploadPublicEvidenceAsync(token, itemId, file.FileName, file.ContentType, content, cancellationToken);
            return result.ToHttpResult();
        })
            .DisableAntiforgery()
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public offboarding");

        return api;
    }
}
