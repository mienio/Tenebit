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

public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        static async Task<IResult> ListAssetStatuses(SettingsService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAssetStatusesAsync(cancellationToken));

        static async Task<IResult> SaveAssetStatuses(IReadOnlyList<SaveAssetStatusSettingRequest> request, SettingsService service, CancellationToken cancellationToken) =>
            (await service.SaveAssetStatusesAsync(request, cancellationToken)).ToHttpResult();

        api.MapGet("/asset-statuses", ListAssetStatuses)
            .WithTags("Settings");

        api.MapGet("/settings/asset-statuses", ListAssetStatuses)
            .WithTags("Settings");

        api.MapPut("/asset-statuses", SaveAssetStatuses)
            .WithTags("Settings");

        api.MapPut("/settings/asset-statuses", SaveAssetStatuses)
            .WithTags("Settings");

        api.MapGet("/settings/evidence-privacy", async (SettingsService service, CancellationToken cancellationToken) =>
                (await service.GetEvidencePrivacyAsync(cancellationToken)).ToHttpResult())
            .WithTags("Settings");

        api.MapPut("/settings/evidence-privacy", async (SaveEvidencePrivacySettingsRequest request, SettingsService service, CancellationToken cancellationToken) =>
                (await service.SaveEvidencePrivacyAsync(request, cancellationToken)).ToHttpResult())
            .WithTags("Settings");

        api.MapGet("/settings/qr-label", async (SettingsService service, CancellationToken cancellationToken) =>
                (await service.GetQrLabelSettingsAsync(cancellationToken)).ToHttpResult())
            .WithTags("Settings");

        api.MapPut("/settings/qr-label", async (SaveQrLabelSettingsRequest request, SettingsService service, CancellationToken cancellationToken) =>
                (await service.SaveQrLabelSettingsAsync(request, cancellationToken)).ToHttpResult())
            .WithTags("Settings");

        api.MapGet("/settings/alerts", async (AlertSettingsService service, CancellationToken cancellationToken) =>
                (await service.ListAlertRulesAsync(cancellationToken)).ToHttpResult())
            .WithTags("Settings");

        api.MapPut("/settings/alerts/{type}", async (AlertType type, SaveAlertRuleRequest request, AlertSettingsService service, CancellationToken cancellationToken) =>
                (await service.UpsertAlertRuleAsync(type, request, cancellationToken)).ToHttpResult())
            .WithTags("Settings");

        api.MapGet("/settings/alert-digest", async (AlertSettingsService service, CancellationToken cancellationToken) =>
                (await service.GetAlertDigestAsync(cancellationToken)).ToHttpResult())
            .WithTags("Settings");

        api.MapPut("/settings/alert-digest", async (SaveAlertDigestSettingsRequest request, AlertSettingsService service, CancellationToken cancellationToken) =>
                (await service.UpsertAlertDigestAsync(request, cancellationToken)).ToHttpResult())
            .WithTags("Settings");

        api.MapPost("/settings/alerts/test", async (AlertTestRequest? request, AlertSettingsService service, CancellationToken cancellationToken) =>
                (await service.SendTestAlertAsync(request, cancellationToken)).ToNoContentResult())
            .WithTags("Settings");

        api.MapGet("/alerts/history", async (int? page, int? pageSize, AlertSettingsService service, CancellationToken cancellationToken) =>
                (await service.ListSentAlertHistoryAsync(page ?? 1, pageSize ?? 25, cancellationToken)).ToHttpResult())
            .WithTags("Alerts");

        static async Task<IResult> ListJobProfiles(JobProfileService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken));

        api.MapGet("/job-profiles", ListJobProfiles)
            .WithTags("Job profiles");

        api.MapGet("/settings/job-profiles", ListJobProfiles)
            .WithTags("Job profiles");

        api.MapPost("/job-profiles", async (SaveJobProfileRequest request, JobProfileService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/job-profiles/{response.Id}"))
            .WithTags("Job profiles");

        api.MapPut("/job-profiles/{id:guid}", async (Guid id, SaveJobProfileRequest request, JobProfileService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Job profiles");

        api.MapDelete("/job-profiles/{id:guid}", async (Guid id, JobProfileService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContentResult())
            .WithTags("Job profiles");

        static async Task<IResult> ListUsers(UserAccessService service, CancellationToken cancellationToken) =>
            (await service.ListAsync(cancellationToken)).ToHttpResult();

        api.MapGet("/organization-users", ListUsers)
            .WithTags("Users");

        api.MapGet("/settings/users", ListUsers)
            .WithTags("Users");

        api.MapPost("/organization-users", async (SaveOrganizationUserRequest request, UserAccessService service, CancellationToken cancellationToken) =>
                (await service.CreateAsync(request, cancellationToken)).ToCreatedResult(response => $"/api/organization-users/{response.Id}"))
            .WithTags("Users");

        api.MapPut("/organization-users/{id:guid}", async (Guid id, SaveOrganizationUserRequest request, UserAccessService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithTags("Users");

        static IResult ListRoles(UserAccessService service) => Results.Ok(service.Roles());

        api.MapGet("/roles", ListRoles)
            .WithTags("Users");

        api.MapGet("/settings/roles", ListRoles)
            .WithTags("Users");

        api.MapGet("/role-permissions", async (RolePermissionService service, CancellationToken cancellationToken) =>
                (await service.ListAsync(cancellationToken)).ToHttpResult())
            .WithTags("Users");

        api.MapPut("/role-permissions", async (SetRolePermissionRequest request, RolePermissionService service, CancellationToken cancellationToken) =>
                (await service.SetAsync(request, cancellationToken)).ToNoContentResult())
            .WithTags("Users");

        return api;
    }
}
