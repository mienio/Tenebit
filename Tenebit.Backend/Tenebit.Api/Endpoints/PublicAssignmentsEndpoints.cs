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

public static class PublicAssignmentsEndpoints
{
    public static RouteGroupBuilder MapPublicAssignmentsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/public/assignments/{token}", async (string token, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.GetPublicAsync(token, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments");

        api.MapPost("/public/assignments/{token}/accept", async (string token, AssignmentService service, CancellationToken cancellationToken) =>
                (await service.AcceptPublicAsync(token, cancellationToken)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments");

        api.MapGet("/public/assignments/{token}/protocol", async (string token, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetPublicProtocolPdfAsync(token, cancellationToken);
            return result.IsFailure || result.Value is null
                ? result.ToHttpResult()
                : Results.File(result.Value, "application/pdf", "protokol.pdf");
        })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments");

        api.MapGet("/public/assignments/{token}/procedures/{procedureId:guid}/documents/{documentId:guid}", async (string token, Guid procedureId, Guid documentId, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetPublicProcedureDocumentAsync(token, procedureId, documentId, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                return result.ToHttpResult();
            }

            var document = result.Value;
            return Results.File(document.Content, document.ContentType, document.FileName);
        })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments");

        api.MapGet("/public/assignments/{token}/evidence/{id:guid}", async (string token, Guid id, AssignmentService assignmentService, AssetEvidenceService evidenceService, CancellationToken cancellationToken) =>
        {
            var resolved = await assignmentService.ResolvePublicTokenAsync(token, cancellationToken);
            if (resolved.IsFailure) return resolved.ToHttpResult();

            var (organizationId, assignmentId) = resolved.Value!;
            var result = await evidenceService.GetPublicAssignmentEvidenceAsync(organizationId, assignmentId, id, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                return result.ToHttpResult();
            }

            var evidence = result.Value;
            return Results.File(evidence.Content, evidence.ContentType, evidence.FileName);
        })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public assignments");

        return api;
    }
}
