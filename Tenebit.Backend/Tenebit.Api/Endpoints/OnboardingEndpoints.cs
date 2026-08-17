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

public static class OnboardingEndpoints
{
    public static RouteGroupBuilder MapOnboardingEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/onboarding/status", async (OnboardingService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetStatusAsync(cancellationToken)))
            .WithTags("Onboarding");

        api.MapPost("/onboarding/starter-package", async (CreateStarterPackageRequest request, OnboardingService service, CancellationToken cancellationToken) =>
                (await service.CreateStarterPackageAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/assignments/{response.AssignmentId}"))
            .WithTags("Onboarding");

        api.MapPost("/onboarding/employee-package", async (CreateEmployeePackageRequest request, OnboardingService service, CancellationToken cancellationToken) =>
                (await service.CreateEmployeePackageAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/assignments/{response.AssignmentId}"))
            .WithTags("Onboarding");

        api.MapPost("/onboarding/employee-package/with-evidence", async (HttpRequest request, OnboardingService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Wyślij żądanie jako multipart/form-data.", code = "VALIDATION_ERROR" });
            }

            MultipartRequestHelpers.LimitRequestBody(request, MultipartRequestHelpers.MaxEvidenceBundleUploadBytes);
            var form = await request.ReadFormAsync(cancellationToken);
            var createRequest = MultipartRequestHelpers.DeserializePart<CreateEmployeePackageRequest>(form, "request");
            if (createRequest is null)
            {
                return Results.BadRequest(new { message = "Pole 'request' musi zawierać poprawny JSON pakietu pracownika.", code = "VALIDATION_ERROR" });
            }

            var manifest = MultipartRequestHelpers.DeserializeManifest(form);
            if (manifest is null)
            {
                return Results.BadRequest(new { message = "Pole 'evidenceManifest' musi zawierać poprawny JSON.", code = "VALIDATION_ERROR" });
            }

            var files = new List<EvidenceFileInput>();
            foreach (var file in form.Files)
            {
                files.Add(new EvidenceFileInput(file.Name, file.FileName, file.ContentType, await MultipartRequestHelpers.ReadFileAsync(file, cancellationToken)));
            }

            return (await service.CreateEmployeePackageWithEvidenceAsync(createRequest, manifest, files, cancellationToken)).ToCreatedResult(response => $"/api/assignments/{response.AssignmentId}");
        })
            .DisableAntiforgery()
            .WithTags("Onboarding");

        api.MapGet("/onboarding/checklist/{personId:guid}", async (Guid personId, OnboardingService service, CancellationToken cancellationToken) =>
                (await service.GetChecklistAsync(personId, cancellationToken)).ToHttpResult())
            .WithTags("Onboarding");

        return api;
    }
}
