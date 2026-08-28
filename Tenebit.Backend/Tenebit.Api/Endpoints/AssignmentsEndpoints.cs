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

public static class AssignmentsEndpoints
{
    public static RouteGroupBuilder MapAssignmentsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/assignments", async (AssignmentService service, string? search, Tenebit.Domain.Assignments.AssignmentStatus? status, int? page, int? pageSize, CancellationToken cancellationToken) =>
                (await service.ListPagedAsync(search, status, page ?? 1, pageSize ?? 25, cancellationToken)).ToHttpResult())
            .WithTags("Assignments");

        api.MapGet("/assignments/{id:guid}", async (Guid id, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Assignments");

        api.MapPost("/assignments", async (CreateAssignmentRequest request, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/assignments/{response.Id}"))
            .WithTags("Assignments");

        api.MapPost("/assignments/{id:guid}/accept", async (Guid id, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.AcceptAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Assignments");

        api.MapGet("/assignments/{id:guid}/protocol.pdf", async (Guid id, Tenebit.Application.Protocols.ProtocolPdfService protocols, CancellationToken cancellationToken) =>
            {
                var result = await protocols.GetAssignmentProtocolAsync(id, cancellationToken);
                return result.IsFailure || result.Value is null ? result.ToHttpResult() : Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
            })
            .WithTags("Assignments");

        api.MapPost("/assignments/{id:guid}/acceptance-link", async (Guid id, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.RegenerateAcceptanceLinkAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Assignments");

        api.MapPost("/assignments/{id:guid}/return", async (Guid id, ReturnAssignmentRequest request, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.ReturnAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Assignments");

        api.MapPost("/assignments/{assignmentId:guid}/assets/{assetId:guid}/return", async (Guid assignmentId, Guid assetId, ReturnAssignmentAssetItemRequest request, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.ReturnAssetAsync(assignmentId, assetId, request, cancellationToken)).ToHttpResult())
            .WithTags("Assignments");

        api.MapPost("/assignments/with-evidence", async (HttpRequest request, AssignmentService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = ResultExtensions.Localize("Wyślij żądanie jako multipart/form-data."), code = "VALIDATION_ERROR" });
            }

            MultipartRequestHelpers.LimitRequestBody(request, MultipartRequestHelpers.MaxEvidenceBundleUploadBytes);
            var form = await request.ReadFormAsync(cancellationToken);
            MultipartRequestHelpers.ValidateEvidenceBundle(form.Files);
            var createRequest = MultipartRequestHelpers.DeserializePart<CreateAssignmentRequest>(form, "request", out var requestError);
            if (createRequest is null)
            {
                return Results.BadRequest(new { message = ResultExtensions.Localize(requestError ?? "Nieprawidłowe dane wydania."), code = "VALIDATION_ERROR" });
            }

            var manifest = MultipartRequestHelpers.DeserializeManifest(form, out var manifestError);
            if (manifest is null)
            {
                return Results.BadRequest(new { message = ResultExtensions.Localize(manifestError ?? "Nieprawidłowy manifest zdjęć."), code = "VALIDATION_ERROR" });
            }

            var files = new List<EvidenceFileInput>();
            foreach (var file in form.Files)
            {
                files.Add(new EvidenceFileInput(file.Name, file.FileName, file.ContentType, await MultipartRequestHelpers.ReadFileAsync(file, RequestSizeLimits.MaxEvidenceFileBytes, cancellationToken)));
            }

            return (await service.CreateWithEvidenceAsync(createRequest, manifest, files, cancellationToken)).ToCreatedResult(response => $"/api/assignments/{response.Id}");
        })
            .DisableAntiforgery()
            .WithTags("Assignments");

        api.MapPost("/assignments/{assignmentId:guid}/assets/{assetId:guid}/return-with-evidence", async (Guid assignmentId, Guid assetId, HttpRequest request, AssignmentService service, CancellationToken cancellationToken) =>
        {
            MultipartRequestHelpers.LimitRequestBody(request, MultipartRequestHelpers.MaxEvidenceBundleUploadBytes);
            var form = await request.ReadFormAsync(cancellationToken);
            MultipartRequestHelpers.ValidateEvidenceBundle(form.Files);
            var returnRequest = MultipartRequestHelpers.DeserializePart<ReturnAssignmentAssetItemRequest>(form, "request", out var requestError);
            if (returnRequest is null)
            {
                return Results.BadRequest(new { message = ResultExtensions.Localize(requestError ?? "Nieprawidłowe dane zwrotu."), code = "VALIDATION_ERROR" });
            }

            var files = new List<EvidenceFileInput>();
            foreach (var file in form.Files)
            {
                files.Add(new EvidenceFileInput(file.Name, file.FileName, file.ContentType, await MultipartRequestHelpers.ReadFileAsync(file, RequestSizeLimits.MaxEvidenceFileBytes, cancellationToken)));
            }

            return (await service.ReturnAssetWithEvidenceAsync(assignmentId, assetId, returnRequest, files, cancellationToken)).ToHttpResult();
        })
            .DisableAntiforgery()
            .WithTags("Assignments");

        return api;
    }
}
