using Microsoft.AspNetCore.RateLimiting;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;

namespace Tenebit.Api.Endpoints;

public static class TenebitEndpoints
{
    public static RouteGroupBuilder MapTenebitApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Tenebit");
        api.RequireAuthorization();
        api.AddEndpointFilter<ValidationEndpointFilter>();

        // A platform-admin token carries no organization_id claim, which would otherwise make every
        // tenant-scoped EF query filter bypass and return cross-organization data (see
        // TenebitDbContext.ConfigureTenantQueryFilter). Block it here explicitly rather than relying on
        // that as the only safeguard - admin tokens are only ever meant to reach /api/admin.
        api.AddEndpointFilter(async (context, next) =>
        {
            if (context.HttpContext.User.HasClaim(PlatformAdminClaims.ScopeClaimType, PlatformAdminClaims.ScopeValue))
            {
                return Results.Forbid();
            }

            return await next(context);
        });

        api.MapGet("/health", () => Results.Ok(new { status = "ok", product = "Tenebit" }))
            .AllowAnonymous()
            .WithName("Health");

        api.MapGet("/health/ready", async (IDatabaseHealthProbe database, ILogger<Program> logger, CancellationToken cancellationToken) =>
            {
                try
                {
                    var canConnect = await database.CanConnectAsync(cancellationToken);
                    return canConnect
                        ? Results.Ok(new { status = "ready", database = "ok" })
                        : Results.Json(new { status = "unready", database = "unreachable" }, statusCode: 503);
                }
                catch (Exception ex)
                {
                    // AUD-013: nie ujawniamy ex.Message anonimowemu klientowi (nazwa hosta DB, tabeli,
                    // błąd uwierzytelnienia) - szczegóły trafiają tylko do chronionego logu z correlation id.
                    logger.LogError(ex, "Health check /health/ready: baza danych nieosiągalna.");
                    return Results.Json(new { status = "unready", database = "error" }, statusCode: 503);
                }
            })
            .AllowAnonymous()
            .WithName("HealthReady");

        api.MapGet("/health/security-metrics", GetSecurityMetrics)
            .RequireAuthorization(policy => policy.RequireRole(TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Auditor))
            .WithName("SecurityMetrics");

        // Quick search for the Ctrl+K palette. Authorization is enforced inside GlobalSearchService by
        // reusing each module's own service, so no extra role check belongs here.
        api.MapGet("/search", async (string? q, Tenebit.Application.Search.GlobalSearchService search, CancellationToken cancellationToken) =>
                Results.Ok(await search.SearchAsync(q, cancellationToken)))
            .WithTags("Search");

        api.MapAuthEndpoints();
        api.MapExternalAuthEndpoints();
        api.MapWorkspaceEndpoints();
        api.MapDashboardEndpoints();
        api.MapOrganizationEndpoints();
        api.MapOnboardingEndpoints();
        api.MapAssetsEndpoints();
        api.MapAssetEvidenceEndpoints();
        api.MapLocationEndpoints();
        api.MapSettingsEndpoints();
        api.MapPeopleEndpoints();
        api.MapProceduresEndpoints();
        api.MapAssignmentsEndpoints();
        api.MapOffboardingEndpoints();
        api.MapAssetAuditsEndpoints();
        api.MapPublicCapabilityEndpoints();
        api.MapPublicAssignmentsEndpoints();
        api.MapPublicOffboardingEndpoints();
        api.MapPublicAssetAuditsEndpoints();
        api.MapPublicAssetsEndpoints();
        api.MapActivityLogEndpoints();
        api.MapSubscriptionEndpoints();
        api.MapLicensesEndpoints();
        api.MapServiceTicketsEndpoints();

        return api;
    }

    private static IResult GetSecurityMetrics(ICurrentUser currentUser)
    {
        var access = AccessPolicy.EnsureAnyRole(currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Auditor);
        return access.IsFailure ? Results.Forbid() : Results.Ok(SecurityTelemetry.Snapshot());
    }
}
