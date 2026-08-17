using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Talks to the Stripe REST API directly (Checkout Sessions, Billing Portal Sessions, webhook signature
/// verification) rather than pulling in the Stripe.NET SDK — keeps the dependency footprint to just
/// HttpClient + System.Text.Json for what is a handful of well-documented endpoints.
///
/// Required configuration (appsettings "Stripe" section / env vars):
///   Stripe:SecretKey     — sk_live_... / sk_test_... secret API key.
///   Stripe:WebhookSecret — whsec_... signing secret from the Stripe webhook endpoint configuration.
///   Stripe:ProPriceId    — price_... id of the recurring monthly price for the Pro plan.
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    private const string ApiBase = "https://api.stripe.com/v1/";

    private static readonly string[] HandledEventTypes =
    [
        "customer.subscription.created",
        "customer.subscription.updated",
        "customer.subscription.deleted"
    ];

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(IConfiguration configuration, ILogger<StripePaymentGateway> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _http = new HttpClient { BaseAddress = new Uri(ApiBase) };
    }

    private string? SecretKey => _configuration["Stripe:SecretKey"];
    private string? WebhookSecret => _configuration["Stripe:WebhookSecret"];
    private string? ProPriceId => _configuration["Stripe:ProPriceId"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(ProPriceId);

    public async Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken cancellationToken)
    {
        var json = await PostAsync("customers", new Dictionary<string, string>
        {
            ["email"] = email,
            ["metadata[organizationId]"] = organizationId.ToString()
        }, cancellationToken);

        return json.GetProperty("id").GetString()!;
    }

    public async Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProPriceId))
        {
            throw new InvalidOperationException("Stripe:ProPriceId is not configured.");
        }

        var json = await PostAsync("checkout/sessions", new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["customer"] = customerId,
            ["client_reference_id"] = organizationId.ToString(),
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["line_items[0][price]"] = ProPriceId,
            ["line_items[0][quantity]"] = "1",
            ["subscription_data[metadata][organizationId]"] = organizationId.ToString()
        }, cancellationToken);

        return json.GetProperty("url").GetString()!;
    }

    public async Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken)
    {
        var json = await PostAsync("billing_portal/sessions", new Dictionary<string, string>
        {
            ["customer"] = customerId,
            ["return_url"] = returnUrl
        }, cancellationToken);

        return json.GetProperty("url").GetString()!;
    }

    public PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader)
    {
        VerifySignature(payload, signatureHeader);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var eventId = root.GetProperty("id").GetString() ?? "";
        var type = root.GetProperty("type").GetString() ?? "";
        var eventCreatedAt = root.TryGetProperty("created", out var createdProp) && createdProp.TryGetInt64(out var createdUnix)
            ? DateTimeOffset.FromUnixTimeSeconds(createdUnix)
            : DateTimeOffset.UtcNow;

        if (!HandledEventTypes.Contains(type))
        {
            return null;
        }

        var obj = root.GetProperty("data").GetProperty("object");
        var customerId = obj.GetProperty("customer").GetString() ?? "";
        var subscriptionId = obj.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var stripeStatus = obj.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var status = MapStatus(type, stripeStatus);

        var currentPeriodStart = obj.TryGetProperty("current_period_start", out var startProp) && startProp.TryGetInt64(out var startUnix)
            ? DateTimeOffset.FromUnixTimeSeconds(startUnix)
            : DateTimeOffset.UtcNow;
        var currentPeriodEnd = obj.TryGetProperty("current_period_end", out var endProp) && endProp.TryGetInt64(out var endUnix)
            ? DateTimeOffset.FromUnixTimeSeconds(endUnix)
            : currentPeriodStart.AddMonths(1);

        Guid? organizationId = obj.TryGetProperty("metadata", out var metadata)
            && metadata.TryGetProperty("organizationId", out var orgProp)
            && Guid.TryParse(orgProp.GetString(), out var parsedOrgId)
                ? parsedOrgId
                : null;

        // Only the Pro plan is ever purchased through Checkout, so any subscription object is Pro
        // (a Cancelled status will make OrganizationSubscription.SyncFromStripe revert to Free regardless).
        return new PaymentWebhookEvent(eventId, type, customerId, subscriptionId, SubscriptionPlan.Pro.Key, status, eventCreatedAt, currentPeriodStart, currentPeriodEnd, organizationId);
    }

    private SubscriptionStatus MapStatus(string eventType, string? stripeStatus)
    {
        if (eventType == "customer.subscription.deleted") return SubscriptionStatus.Cancelled;

        return stripeStatus switch
        {
            "active" or "trialing" => SubscriptionStatus.Active,
            "past_due" or "incomplete" => SubscriptionStatus.PastDue,
            "canceled" or "unpaid" or "incomplete_expired" => SubscriptionStatus.Cancelled,
            // Nieznany status Stripe nie może po cichu odblokować płatnego planu (fail-open) — traktujemy go
            // konserwatywnie jak PastDue (nie odbiera już przyznanego dostępu, ale i nie potwierdza nowego)
            // i logujemy do przeglądu (audyt P0.6).
            _ => LogUnknownStatusAndFallBack(stripeStatus)
        };
    }

    private SubscriptionStatus LogUnknownStatusAndFallBack(string? stripeStatus)
    {
        _logger.LogWarning("Nieznany status subskrypcji Stripe {StripeStatus} — zastosowano konserwatywny fallback PastDue.", stripeStatus);
        return SubscriptionStatus.PastDue;
    }

    private void VerifySignature(string payload, string signatureHeader)
    {
        var secret = WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Stripe:WebhookSecret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            throw new InvalidOperationException("Missing Stripe-Signature header.");
        }

        string? timestamp = null;
        string? signature = null;
        foreach (var part in signatureHeader.Split(','))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "t") timestamp = pair[1];
            else if (pair[0] == "v1") signature = pair[1];
        }

        if (timestamp is null || signature is null)
        {
            throw new InvalidOperationException("Malformed Stripe-Signature header.");
        }

        // Bez tolerancji czasowej ważny podpis pozostaje wiecznie ważny — przechwycony kiedyś payload
        // dałoby się odtworzyć (replay) w dowolnym momencie w przyszłości (audyt P0.6). 5 minut to
        // domyślna tolerancja rekomendowana przez Stripe.
        if (!long.TryParse(timestamp, out var timestampUnix))
        {
            throw new InvalidOperationException("Malformed Stripe-Signature timestamp.");
        }

        var eventTime = DateTimeOffset.FromUnixTimeSeconds(timestampUnix);
        if ((DateTimeOffset.UtcNow - eventTime).Duration() > TimeSpan.FromMinutes(5))
        {
            throw new InvalidOperationException("Stripe webhook signature timestamp is outside the allowed tolerance.");
        }

        var signedPayload = $"{timestamp}.{payload}";
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature)))
        {
            throw new InvalidOperationException("Invalid Stripe webhook signature.");
        }
    }

    private async Task<JsonElement> PostAsync(string path, IEnumerable<KeyValuePair<string, string>> form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException("Stripe:SecretKey is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Stripe API error {StatusCode} calling {Path}: {Body}", response.StatusCode, path, body);
            throw new InvalidOperationException($"Stripe API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }
}
