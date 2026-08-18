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

public static class ProceduresEndpoints
{
    public static RouteGroupBuilder MapProceduresEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/procedures", async (ProcedureService service, string? search, int? page, int? pageSize, CancellationToken cancellationToken) =>
                page.HasValue
                    ? (await service.ListPagedAsync(search, page.Value, pageSize ?? 25, cancellationToken)).ToHttpResult()
                    : (await service.ListAsync(search, cancellationToken)).ToHttpResult())
            .WithTags("Procedures");

        api.MapGet("/procedures/{id:guid}", async (Guid id, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Procedures");

        api.MapPost("/procedures", async (CreateProcedureRequest request, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/procedures/{response.Id}"))
            .WithTags("Procedures");

        api.MapPut("/procedures/{id:guid}", async (Guid id, UpdateProcedureRequest request, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Procedures");

        api.MapPost("/procedures/{id:guid}/documents", async (Guid id, HttpRequest request, ProcedureService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Wyślij plik jako multipart/form-data.", code = "VALIDATION_ERROR" });
            }

            MultipartRequestHelpers.LimitRequestBody(request, MultipartRequestHelpers.MaxProcedureDocumentUploadBytes);
            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return Results.BadRequest(new { message = "Wybierz plik procedury.", code = "VALIDATION_ERROR" });
            }

            if (file.Length == 0)
            {
                return Results.BadRequest(new { message = "Plik procedury jest pusty.", code = "VALIDATION_ERROR" });
            }

            var content = await MultipartRequestHelpers.ReadFileAsync(file, RequestSizeLimits.MaxProcedureDocumentFileBytes, cancellationToken);
            return (await service.AttachDocumentAsync(id, file.FileName, file.ContentType, content, cancellationToken)).ToHttpResult();
        })
            .DisableAntiforgery()
            .WithTags("Procedures");

        api.MapGet("/procedures/{id:guid}/documents/{documentId:guid}", async (Guid id, Guid documentId, ProcedureService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetDocumentAsync(id, documentId, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                return result.ToHttpResult();
            }

            var document = result.Value;
            return Results.File(document.Content, document.ContentType, document.FileName);
        })
            .WithTags("Procedures");

        api.MapDelete("/procedures/{id:guid}/documents/{documentId:guid}", async (Guid id, Guid documentId, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.RemoveDocumentAsync(id, documentId, cancellationToken)).ToHttpResult())
            .WithTags("Procedures");

        api.MapPost("/procedures/{id:guid}/publish", async (Guid id, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.PublishAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Procedures");

        api.MapPost("/procedures/{id:guid}/archive", async (Guid id, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.ArchiveAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Procedures");

        api.MapGet("/procedures/{id:guid}/acceptances", async (Guid id, ProcedureService service, CancellationToken cancellationToken) =>
                (await service.GetAcceptanceStatusAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Procedures");

        return api;
    }
}
