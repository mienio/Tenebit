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

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", async (DashboardService service, CancellationToken cancellationToken) =>
                (await service.GetSummaryAsync(cancellationToken)).ToHttpResult())
            .WithTags("Dashboard");

        api.MapGet("/dashboard/layout", async (DashboardService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetLayoutAsync(cancellationToken)))
            .WithTags("Dashboard");

        api.MapPut("/dashboard/layout", async (SaveDashboardLayoutRequest request, DashboardService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.SaveLayoutAsync(request, cancellationToken)))
            .WithTags("Dashboard");

        api.MapGet("/dashboard/comparison", async (int? daysAgo, DashboardService service, CancellationToken cancellationToken) =>
                (await service.GetComparisonAsync(daysAgo ?? 7, cancellationToken)).ToHttpResult())
            .WithTags("Dashboard");

        return api;
    }
}
