using Microsoft.AspNetCore.RateLimiting;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Common;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Api.Endpoints;

public static class TenebitEndpoints
{
    public static RouteGroupBuilder MapTenebitApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Tenebit");
        api.RequireAuthorization();
        api.AddEndpointFilter<ValidationEndpointFilter>();

        api.MapGet("/health", () => Results.Ok(new { status = "ok", product = "Tenebit" }))
            .AllowAnonymous()
            .WithName("Health");

        api.MapGet("/health/ready", async (TenebitDbContext db, ILogger<Program> logger, CancellationToken cancellationToken) =>
            {
                try
                {
                    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                    return canConnect
                        ? Results.Ok(new { status = "ready", database = "ok" })
                        : Results.Json(new { status = "unready", database = "unreachable" }, statusCode: 503);
                }
                catch (Exception ex)
                {
                    // AUD-013: nie ujawniamy ex.Message anonimowemu klientowi (nazwa hosta DB, tabeli,
                    // błąd uwierzytelnienia) — szczegóły trafiają tylko do chronionego logu z correlation id.
                    logger.LogError(ex, "Health check /health/ready: baza danych nieosiągalna.");
                    return Results.Json(new { status = "unready", database = "error" }, statusCode: 503);
                }
            })
            .AllowAnonymous()
            .WithName("HealthReady");

        api.MapGet("/health/security-metrics", GetSecurityMetrics)
            .WithName("SecurityMetrics");

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
