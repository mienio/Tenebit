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

        api.MapPost("/subscription/checkout", async (CheckoutSessionRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.CreateCheckoutSessionAsync(request.PlanKey, request.SuccessUrl, request.CancelUrl, cancellationToken)).ToHttpResult())
            .WithTags("Subscription");

        api.MapPost("/subscription/billing-portal", async (BillingPortalRequest request, SubscriptionService service, CancellationToken cancellationToken) =>
                (await service.CreateBillingPortalSessionAsync(request.ReturnUrl, cancellationToken)).ToHttpResult())
            .WithTags("Subscription");

        api.MapPost("/subscription/webhook", async (HttpRequest httpRequest, SubscriptionService service, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(httpRequest.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signature = httpRequest.Headers["Stripe-Signature"].ToString();
                return (await service.HandleWebhookAsync(payload, signature, cancellationToken)).ToNoContentResult();
            })
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithTags("Subscription");

        return api;
    }

    [ValidatedRequest]
    private sealed record UpgradeRequest(string PlanKey);
    [ValidatedRequest]
    private sealed record CheckoutSessionRequest(string PlanKey, string SuccessUrl, string CancelUrl);
    [ValidatedRequest]
    private sealed record BillingPortalRequest(string ReturnUrl);
}
