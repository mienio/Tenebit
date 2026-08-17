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

public static class AssetAuditsEndpoints
{
    public static RouteGroupBuilder MapAssetAuditsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/asset-audits", async (AssetAuditCampaignStatus? status, int? page, int? pageSize, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.ListPagedAsync(status, page ?? 1, pageSize ?? 25, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapGet("/asset-audits/{id:guid}", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapPost("/asset-audits", async (CreateAssetAuditCampaignRequest request, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/asset-audits/{response.Campaign.Id}"))
            .WithTags("Asset audits");

        api.MapPut("/asset-audits/{id:guid}", async (Guid id, UpdateAssetAuditCampaignRequest request, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapPost("/asset-audits/{id:guid}/preview", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.PreviewAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapPost("/asset-audits/{id:guid}/start", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.StartAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapPost("/asset-audits/{id:guid}/remind", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.RemindParticipantsAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapPost("/asset-audits/{id:guid}/participants/{participantId:guid}/reopen", async (Guid id, Guid participantId, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.ReopenParticipantAsync(id, participantId, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapPost("/asset-audits/{id:guid}/items/{itemId:guid}/resolve", async (Guid id, Guid itemId, ResolveAssetAuditItemRequest request, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.ResolveItemAsync(id, itemId, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapPost("/asset-audits/{id:guid}/complete", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.CompleteAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapPost("/asset-audits/{id:guid}/cancel", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
                (await service.CancelAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Asset audits");

        api.MapGet("/asset-audits/{id:guid}/export.csv", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ExportCsvAsync(id, cancellationToken);
            return result.IsFailure || result.Value is null
                ? result.ToHttpResult()
                : Results.File(System.Text.Encoding.UTF8.GetBytes(result.Value), "text/csv", $"audyt-{id}.csv");
        })
            .WithTags("Asset audits");

        api.MapGet("/asset-audits/{id:guid}/report.pdf", async (Guid id, AssetAuditCampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetReportPdfAsync(id, cancellationToken);
            return result.IsFailure || result.Value is null
                ? result.ToHttpResult()
                : Results.File(result.Value, "application/pdf", $"raport-audytu-{id}.pdf");
        })
            .WithTags("Asset audits");

        return api;
    }
}
