using Tenebit.Api.Auth;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Assignments;
using Tenebit.Application.Audits;
using Tenebit.Application.Common;
using Tenebit.Application.Offboarding;

namespace Tenebit.Api.Endpoints;

[ValidatedRequest]
public sealed record PublicCapabilityExchangeRequest(string Purpose, string Token);

public static class PublicCapabilityEndpoints
{
    public static RouteGroupBuilder MapPublicCapabilityEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/public/capability-session", async (
            PublicCapabilityExchangeRequest request,
            AssignmentService assignments,
            OffboardingService offboarding,
            AssetAuditCampaignService audits,
            IPublicCapabilitySessionProtector protector,
            IClock clock,
            IWebHostEnvironment environment,
            HttpResponse response,
            CancellationToken cancellationToken) =>
        {
            if (request.Token.Length > RequestLimits.Token)
                return Results.NotFound();

            var valid = request.Purpose switch
            {
                PublicCapabilityCookie.AssignmentPurpose => (await assignments.GetPublicAsync(request.Token, cancellationToken)).IsSuccess,
                PublicCapabilityCookie.OffboardingPurpose => (await offboarding.GetPublicAsync(request.Token, cancellationToken)).IsSuccess,
                PublicCapabilityCookie.AssetAuditPurpose => (await audits.GetPublicAsync(request.Token, cancellationToken)).IsSuccess,
                _ => false
            };
            if (!valid) return Results.NotFound();

            PublicCapabilityCookie.Issue(response, protector, request.Purpose, request.Token, clock.UtcNow, environment.IsDevelopment());
            return Results.NoContent();
        })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Public capability");

        return api;
    }
}
