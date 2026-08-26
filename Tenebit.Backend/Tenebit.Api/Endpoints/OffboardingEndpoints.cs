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

public static class OffboardingEndpoints
{
    public static RouteGroupBuilder MapOffboardingEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/offboarding", async (OffboardingCaseStatus? status, int? page, int? pageSize, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ListPagedAsync(status, page ?? 1, pageSize ?? 25, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapGet("/offboarding/{id:guid}", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding", async (CreateOffboardingCaseRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/offboarding/{response.Case.Id}"))
            .WithTags("Offboarding");

        api.MapPut("/offboarding/{id:guid}", async (Guid id, UpdateOffboardingCaseRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/start", async (Guid id, StartOffboardingCaseRequest? request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.StartAsync(id, request ?? new StartOffboardingCaseRequest(), cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/resend", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ResendLinkAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/regenerate-link", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.RegenerateLinkAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/execute-scheduled-actions", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ExecuteScheduledActionsAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/confirm-return", async (Guid id, Guid itemId, ConfirmOffboardingItemReturnRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ConfirmItemReturnAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/complete-inspection", async (Guid id, Guid itemId, CompleteAssetInspectionRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.CompleteItemInspectionAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/release-license", async (Guid id, Guid itemId, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ReleaseItemLicenseAsync(id, itemId, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/resolve", async (Guid id, Guid itemId, ResolveOffboardingItemRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.ResolveItemAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/items/{itemId:guid}/waive", async (Guid id, Guid itemId, WaiveOffboardingItemRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.WaiveItemAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/complete", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.CompleteAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapGet("/offboarding/{id:guid}/protocol.pdf", async (Guid id, Tenebit.Application.Protocols.ProtocolPdfService protocols, CancellationToken cancellationToken) =>
            {
                var result = await protocols.GetOffboardingProtocolAsync(id, cancellationToken);
                return result.IsFailure || result.Value is null ? result.ToHttpResult() : Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
            })
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/cancel", async (Guid id, CancelOffboardingCaseRequest request, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.CancelAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        api.MapPost("/offboarding/{id:guid}/restore-employment", async (Guid id, OffboardingService service, CancellationToken cancellationToken) =>
                (await service.RestoreEmploymentAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Offboarding");

        return api;
    }
}
