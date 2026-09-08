using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
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

public static class SubscriptionEndpoints
{
    public static RouteGroupBuilder MapSubscriptionEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/subscription", async (SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.GetCurrentAsync(cancellationToken)).ToHttpResult())
            .WithTags("Subscription");

        api.MapPost("/subscription/upgrade", async (UpgradeRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.UpgradeAsync(request.PlanKey, cancellationToken)).ToHttpResult())
            .WithTags("Subscription");

        api.MapPost("/subscription/checkout-params", async (CheckoutParamsRequest request, HttpContext http, SubscriptionService service, CancellationToken cancellationToken) =>
            {
                var attributionToken = http.Request.Cookies.TryGetValue(RedirectEndpoints.AttributionCookieName, out var raw) && Guid.TryParse(raw, out var parsed)
                    ? parsed
                    : (Guid?)null;
                return (await service.GetCheckoutParamsAsync(request.PlanKey, cancellationToken, request.PromoCode, attributionToken)).ToHttpResult();
            })
            .RequireRateLimiting("code-guess")
            .WithTags("Subscription");

        api.MapPost("/subscription/change-plan", async (ChangePlanRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.ChangePlanAsync(request.PlanKey, cancellationToken, request.PromoCode)).ToHttpResult())
            .RequireRateLimiting("code-guess")
            .WithTags("Subscription");

        api.MapPost("/subscription/change-plan/preview", async (ChangePlanRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.PreviewPlanChangeAsync(request.PlanKey, cancellationToken)).ToHttpResult())
            .WithTags("Subscription");

        api.MapPost("/subscription/cancel-scheduled-change", async (SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.CancelScheduledPlanChangeAsync(cancellationToken)).ToHttpResult())
            .WithTags("Subscription");

        api.MapPost("/subscription/promo-code/validate", async (PromoCodeValidateRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.ValidatePromoCodeAsync(request.PlanKey, request.Code, cancellationToken)).ToHttpResult())
            .RequireRateLimiting("code-guess")
            .WithTags("Subscription");

        api.MapPost("/subscription/billing-portal", async (SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.CreateCustomerPortalSessionAsync(cancellationToken)).ToHttpResult())
            .WithTags("Subscription");

        // Public, unauthenticated config Paddle.js needs to initialize in the browser - a client-side
        // token, not a secret (same trust level Stripe's pk_... publishable key had).
        api.MapGet("/subscription/paddle-config", (IConfiguration configuration) =>
                Results.Ok(new PaddleClientConfig(
                    configuration["Paddle:ClientSideToken"] ?? "",
                    string.Equals(configuration["Paddle:Environment"], "production", StringComparison.OrdinalIgnoreCase) ? "production" : "sandbox")))
            .AllowAnonymous()
            .WithTags("Subscription");

        api.MapPost("/subscription/webhook", async (
                HttpRequest httpRequest, SubscriptionService service, Tenebit.Application.Affiliates.AffiliateConversionRecordingService affiliateConversions, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(httpRequest.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signature = httpRequest.Headers["Paddle-Signature"].ToString();
                var result = await service.HandleWebhookAsync(payload, signature, cancellationToken);
                // Paddle delivers every event type (subscription.* and transaction.completed alike) to this
                // one configured URL - the affiliate commission side reads the same raw payload independently
                // (see AffiliateConversionRecordingService.HandleWebhookAsync) rather than being threaded
                // through SubscriptionService, so both run off a single POST regardless of which one applies.
                if (result.IsSuccess) await affiliateConversions.HandleWebhookAsync(payload, signature, cancellationToken);
                return result.ToNoContentResult();
            })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Subscription");

        return api;
    }

    [ValidatedRequest]
    private sealed record UpgradeRequest(string PlanKey);
    [ValidatedRequest]
    private sealed record CheckoutParamsRequest(string PlanKey, string? PromoCode);
    [ValidatedRequest]
    private sealed record ChangePlanRequest(string PlanKey, string? PromoCode);
    [ValidatedRequest]
    private sealed record PromoCodeValidateRequest(string PlanKey, string Code);

    private sealed record PaddleClientConfig(string ClientToken, string Environment);
}
