using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Assignments;
using Tenebit.Application.Evidence;
using Tenebit.Application.Protocols;

namespace Tenebit.Api.Endpoints;

public static class PublicAssignmentsEndpoints
{
    public static RouteGroupBuilder MapPublicAssignmentsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/public/assignments", async (HttpRequest request, AssignmentService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssignmentPurpose, clock.UtcNow);
            return token is null ? Results.NotFound() : (await service.GetPublicAsync(token, ct)).ToHttpResult();
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public assignments");

        api.MapPost("/public/assignments/accept", async (HttpRequest request, AssignmentService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssignmentPurpose, clock.UtcNow);
            return token is null ? Results.NotFound() : (await service.AcceptPublicAsync(token, ct)).ToHttpResult();
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public assignments");

        // Egzemplarz protokołu dla pracownika. Ten sam dokument, który widzi organizacja - pracownik ma
        // wyjść z procesu z własną kopią, a nie tylko z ekranem "potwierdzone".
        api.MapGet("/public/assignments/protocol.pdf", async (HttpRequest request, AssignmentService assignments, ProtocolPdfService protocols, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssignmentPurpose, clock.UtcNow);
            if (token is null) return Results.NotFound();
            var resolved = await assignments.ResolvePublicTokenAsync(token, ct);
            if (resolved.IsFailure) return resolved.ToHttpResult();
            var result = await protocols.GetPublicAssignmentProtocolAsync(resolved.Value!.OrganizationId, resolved.Value.AssignmentId, ct);
            return result.IsFailure || result.Value is null ? result.ToHttpResult() : Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public assignments");

        api.MapGet("/public/assignments/procedures/{procedureId:guid}/documents/{documentId:guid}", async (Guid procedureId, Guid documentId, HttpRequest request, AssignmentService service, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssignmentPurpose, clock.UtcNow);
            if (token is null) return Results.NotFound();
            var result = await service.GetPublicProcedureDocumentAsync(token, procedureId, documentId, ct);
            return result.IsFailure || result.Value is null ? result.ToHttpResult() : Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public assignments");

        api.MapGet("/public/assignments/evidence/{id:guid}", async (Guid id, HttpRequest request, AssignmentService assignments, AssetEvidenceService evidence, IPublicCapabilitySessionProtector protector, IClock clock, CancellationToken ct) =>
        {
            var token = PublicCapabilityCookie.Read(request, protector, PublicCapabilityCookie.AssignmentPurpose, clock.UtcNow);
            if (token is null) return Results.NotFound();
            var resolved = await assignments.ResolvePublicTokenAsync(token, ct);
            if (resolved.IsFailure) return resolved.ToHttpResult();
            var result = await evidence.GetPublicAssignmentEvidenceAsync(resolved.Value!.OrganizationId, resolved.Value.AssignmentId, id, ct);
            return result.IsFailure || result.Value is null ? result.ToHttpResult() : Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }).AllowAnonymous().RequireRateLimiting("public").WithTags("Public assignments");

        return api;
    }
}
