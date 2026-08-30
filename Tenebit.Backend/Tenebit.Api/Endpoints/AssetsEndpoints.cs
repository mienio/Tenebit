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

public static class AssetsEndpoints
{
    public static RouteGroupBuilder MapAssetsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/maintenance", async (MaintenanceService service, CancellationToken ct) =>
                (await service.ListAsync(ct)).ToHttpResult())
            .WithTags("Maintenance");

        api.MapGet("/maintenance/due", async (int? days, MaintenanceService service, CancellationToken ct) =>
                (await service.ListDueAsync(days ?? 90, ct)).ToHttpResult())
            .WithTags("Maintenance");

        api.MapPost("/maintenance", async (SaveMaintenanceScheduleRequest request, MaintenanceService service, CancellationToken ct) =>
                (await service.CreateAsync(request, ct)).ToCreatedResult(response => $"/api/maintenance/{response.Id}"))
            .WithTags("Maintenance");

        api.MapPost("/maintenance/{id:guid}/complete", async (Guid id, CompleteMaintenanceRequest request, MaintenanceService service, CancellationToken ct) =>
                (await service.CompleteAsync(id, request, ct)).ToHttpResult())
            .WithTags("Maintenance");

        api.MapDelete("/maintenance/{id:guid}", async (Guid id, MaintenanceService service, CancellationToken ct) =>
                (await service.DeleteAsync(id, ct)).ToNoContentResult())
            .WithTags("Maintenance");

        api.MapGet("/assets/fleet-value", async (AssetService service, CancellationToken cancellationToken) =>
                (await service.GetFleetValueAsync(cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        api.MapGet("/asset-categories", async (AssetCategoryService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(cancellationToken)))
            .WithTags("Asset categories");

        api.MapPost("/asset-categories", async (CreateAssetCategoryRequest request, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/asset-categories/{response.Id}"))
            .WithTags("Asset categories");

        api.MapPut("/asset-categories/{id:guid}", async (Guid id, UpdateAssetCategoryRequest request, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset categories");

        api.MapDelete("/asset-categories/{id:guid}", async (Guid id, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Asset categories");

        api.MapPut("/asset-categories/{id:guid}/fields", async (Guid id, IReadOnlyList<SaveAssetFieldDefinitionRequest> request, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.SaveFieldDefinitionsAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset categories");

        api.MapPut("/asset-categories/{id:guid}/return-policy", async (Guid id, UpdateAssetCategoryReturnPolicyRequest request, AssetCategoryService service, CancellationToken cancellationToken) =>
                (await service.UpdateReturnPolicyAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Asset categories");

        api.MapGet("/assets", async (AssetService service, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, string? owner, string? warranty, string? sort, bool? desc, int? page, int? pageSize, CancellationToken cancellationToken) =>
                page.HasValue
                    ? (await service.ListPagedAsync(search, status, location, teamId, categoryId, owner == "none", warranty == "expiring", sort, desc ?? false, page.Value, pageSize ?? 25, cancellationToken)).ToHttpResult()
                    : (await service.ListAsync(search, status, location, cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        api.MapGet("/assets/group-counts", async (AssetService service, CancellationToken cancellationToken) =>
                (await service.GetGroupCountsAsync(cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        api.MapGet("/assets/export.csv", async (AssetService service, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool? unassignedOnly, bool? warrantyExpiring, string? sort, bool? desc, string? columns, CancellationToken cancellationToken) =>
        {
            var result = await service.ExportCsvAsync(search, status, location, teamId, categoryId, unassignedOnly ?? false, warrantyExpiring ?? false, sort, desc ?? false, columns, cancellationToken);
            return result.IsFailure || result.Value is null
                ? result.ToHttpResult()
                : Results.File(System.Text.Encoding.UTF8.GetBytes(result.Value), "text/csv", "assets.csv");
        })
            .WithTags("Assets");

        api.MapGet("/assets/export.json", async (AssetService service, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool? unassignedOnly, bool? warrantyExpiring, string? sort, bool? desc, CancellationToken cancellationToken) =>
        {
            var result = await service.ExportJsonAsync(search, status, location, teamId, categoryId, unassignedOnly ?? false, warrantyExpiring ?? false, sort, desc ?? false, cancellationToken);
            return result.IsFailure || result.Value is null
                ? result.ToHttpResult()
                : Results.File(System.Text.Encoding.UTF8.GetBytes(result.Value), "application/json", "assets.json");
        })
            .WithTags("Assets");

        api.MapGet("/assets/{id:guid}", async (Guid id, AssetService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToHttpResult())
            .WithName("GetAsset")
            .WithTags("Assets");

        api.MapPost("/assets", async (CreateAssetRequest request, AssetService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/assets/{response.Id}"))
            .WithTags("Assets");

        api.MapPost("/assets/batch", async (CreateAssetBatchRequest request, AssetService service, CancellationToken cancellationToken) =>
                (await service.CreateBatchAsync(request, cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        api.MapPut("/assets/{id:guid}", async (Guid id, UpdateAssetRequest request, AssetService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        api.MapDelete("/assets/{id:guid}", async (Guid id, AssetService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Assets");

        api.MapGet("/assets/scan/{scanCode}", async (string scanCode, AssetService service, CancellationToken cancellationToken) =>
                (await service.ResolveScanCodeAsync(scanCode, cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        api.MapGet("/assets/{id:guid}/qr", async (Guid id, AssetService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetQrSvgAsync(id, cancellationToken);
            return result.IsSuccess && result.Value is not null
                ? Results.Text(result.Value, "image/svg+xml")
                : result.ToHttpResult();
        })
            .WithTags("Assets");

        api.MapPost("/assets/{id:guid}/fields/{fieldKey}/reveal", async (Guid id, string fieldKey, AssetService service, CancellationToken cancellationToken) =>
                (await service.RevealSensitiveFieldAsync(id, fieldKey, cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        api.MapGet("/assets/{id:guid}/inspection", async (Guid id, AssetInspectionService service, CancellationToken cancellationToken) =>
                (await service.GetPendingForAssetAsync(id, cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        api.MapPost("/assets/inspections/{id:guid}/complete", async (Guid id, CompleteAssetInspectionRequest request, AssetInspectionService service, CancellationToken cancellationToken) =>
                (await service.CompleteAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Assets");

        return api;
    }
}
