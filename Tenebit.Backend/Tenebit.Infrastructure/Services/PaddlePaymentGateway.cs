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
/// Paddle is a Merchant of Record: unlike Stripe (a pure payment processor), Paddle is the legal seller
/// to the end customer - it calculates and remits VAT/sales tax worldwide and issues the actual invoice
/// itself, which is the whole reason this replaced <c>StripePaymentGateway</c> (see paddle_plan.md at the
/// repo root for the full rationale and migration plan).
///
/// Architecturally the biggest difference from Stripe: there is no server-generated hosted checkout
/// redirect URL. New subscriptions are purchased through Paddle.js running client-side (see
/// <see cref="GetCheckoutParamsAsync"/>); everything else (plan changes, portal, webhooks, invoices) is a
/// normal server-to-server REST call, same shape as Stripe.
/// </summary>
public sealed class PaddlePaymentGateway : IPaymentGateway
{
    private const int MaxResponseBytes = 1024 * 1024;
    private static readonly HashSet<string> Handled = new(StringComparer.Ordinal)
    {
        "subscription.created",
        "subscription.updated",
        "subscription.canceled"
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaddlePaymentGateway> _logger;

    public PaddlePaymentGateway(HttpClient http, IConfiguration configuration, ILogger<PaddlePaymentGateway> logger)
    {
        _http = http;
        // Sandbox and production are entirely separate Paddle accounts with separate API keys and
        // separate base URLs - unlike Stripe, where test/live mode shares one endpoint and is selected by
        // which secret key you send.
        _http.BaseAddress ??= new Uri(IsProduction(configuration) ? "https://api.paddle.com/" : "https://sandbox-api.paddle.com/");
        _http.Timeout = TimeSpan.FromSeconds(15);
        _configuration = configuration;
        _logger = logger;
    }

    private static bool IsProduction(IConfiguration configuration) =>
        string.Equals(configuration["Paddle:Environment"], "production", StringComparison.OrdinalIgnoreCase);

    private string? ApiKey => _configuration["Paddle:ApiKey"];
    private string? ClientSideToken => _configuration["Paddle:ClientSideToken"];
    private string? WebhookSecret => _configuration["Paddle:WebhookSecret"];

    // One Paddle Price per paid plan, configured under Paddle:Prices:<planKey> (e.g. Paddle:Prices:business).
    // The Free plan never has a price - it's never looked up here.
    private IReadOnlyDictionary<string, string> PlanPrices => _configuration.GetSection("Paddle:Prices")
        .GetChildren()
        .Where(x => !string.IsNullOrWhiteSpace(x.Value))
        .ToDictionary(x => x.Key, x => x.Value!, StringComparer.Ordinal);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ClientSideToken) &&
        !string.IsNullOrWhiteSpace(WebhookSecret) &&
        PlanPrices.Count > 0;

    public bool IsPlanConfigured(string planKey) => PlanPrices.ContainsKey(planKey);

    public async Task<string> CreateCustomerAsync(string email, Guid organizationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        // Paddle has no documented per-request idempotency-key header equivalent to Stripe's - a retried
        // call here can create a second Customer object. Bounded by SubscriptionService's own
        // checkout-attempt dedup (GetOrCreateCheckoutAttempt) and by the fact this is only ever called once
        // per organization (guarded by PaddleCustomerId already being set), same residual risk window Stripe
        // itself would have without the header.
        var json = await PostJsonAsync(
            "customers",
            new Dictionary<string, object?>
            {
                ["email"] = email,
                ["custom_data"] = new Dictionary<string, object?> { ["organizationId"] = organizationId.ToString() }
            },
            cancellationToken);
        return RequiredString(json, "id");
    }

    public async Task<PaddleCheckoutParams> GetCheckoutParamsAsync(string customerId, string planKey, CancellationToken cancellationToken, PromoCodeDiscount? discount = null)
    {
        if (!PlanPrices.TryGetValue(planKey, out var priceId))
            throw new PaymentGatewayException($"Paddle:Prices:{planKey} is not configured.");

        string? discountId = discount is null ? null : await EnsureDiscountAsync(discount, priceId, cancellationToken);
        return new PaddleCheckoutParams(priceId, customerId, discountId);
    }

    /// <summary>
    /// Our promo codes are our own marketing entities, not Paddle objects - a Paddle Discount only needs to
    /// exist long enough to be referenced by id from the checkout/subscription-update call that applies it.
    /// Unlike Stripe (which built in idempotency-key-guarded coupon reuse to avoid piling up duplicate
    /// Coupon objects), Paddle has no equivalent guarantee, so this simply creates a fresh Discount every
    /// time - a few unused Discount objects accumulating in the Paddle dashboard is harmless clutter, unlike
    /// a failed Stripe idempotency-key body mismatch.
    /// </summary>
    private async Task<string> EnsureDiscountAsync(PromoCodeDiscount discount, string priceId, CancellationToken cancellationToken)
    {
        // "enabled" is not a field Paddle's create-discount endpoint accepts (verified against the real
        // sandbox API: "Additional property enabled is not allowed") - a new discount is active by
        // default, nothing to opt into.
        var body = new Dictionary<string, object?>
        {
            ["description"] = "Tenebit promo code",
            ["restrict_to"] = new[] { priceId },
            ["recur"] = discount.DurationType != PromoDurationType.Once
        };

        // Once = a single charge (recur:false above, no interval count needed). Repeating = keep applying
        // for exactly DurationInMonths renewals. Forever = keep applying with no cap - simply omitting
        // maximum_recurring_intervals is how Paddle represents "unlimited" for a recurring discount.
        if (discount.DurationType == PromoDurationType.Repeating)
            body["maximum_recurring_intervals"] = discount.DurationInMonths;

        if (discount.Type == PromoDiscountType.Percentage)
        {
            body["type"] = "percentage";
            body["amount"] = discount.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            body["type"] = "flat";
            body["amount"] = ((long)Math.Round(discount.Value * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            body["currency_code"] = "EUR";
        }

        var json = await PostJsonAsync("discounts", body, cancellationToken);
        return RequiredString(json, "id");
    }

    public async Task<string> CreateCustomerPortalSessionAsync(string customerId, string? subscriptionId, CancellationToken cancellationToken)
    {
        var body = string.IsNullOrWhiteSpace(subscriptionId)
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?> { ["subscription_ids"] = new[] { subscriptionId } };

        var json = await PostJsonAsync($"customers/{Uri.EscapeDataString(customerId)}/portal-sessions", body, cancellationToken);
        // TODO(paddle-sandbox): confirm the exact nested shape once a real sandbox session is available -
        // deep-linking straight to a specific subscription's management screen (payment method / cancel)
        // instead of the portal's general overview may live under urls.subscriptions[] rather than here.
        if (json.TryGetProperty("urls", out var urls) && urls.TryGetProperty("general", out var general) && general.TryGetProperty("overview", out var overview) && overview.ValueKind == JsonValueKind.String)
            return overview.GetString()!;

        throw new PaymentGatewayException("Paddle customer portal session had no overview URL.");
    }

    /// <summary>Read-only lookup used by the admin panel to show what an organization has actually paid
    /// (see AdminOverviewService.GetOrganizationPaymentsAsync) - this never feeds Tenebit's own billing
    /// state, so it deliberately doesn't go through ParseWebhookEvent/PaymentSubscriptionState.</summary>
    public async Task<IReadOnlyList<PaymentInvoice>> ListInvoicesAsync(string customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return [];

        var json = await GetJsonAsync($"transactions?customer_id={Uri.EscapeDataString(customerId)}&per_page=100&order_by=billed_at[DESC]", cancellationToken);
        if (json.ValueKind != JsonValueKind.Array) return [];

        var invoices = new List<PaymentInvoice>(json.GetArrayLength());
        foreach (var obj in json.EnumerateArray())
        {
            var id = RequiredString(obj, "id");
            var status = obj.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "unknown" : "unknown";
            var currency = obj.TryGetProperty("currency_code", out var currencyProp) ? (currencyProp.GetString() ?? "EUR").ToUpperInvariant() : "EUR";
            var total = obj.TryGetProperty("details", out var details) && details.TryGetProperty("totals", out var totals)
                ? ReadAmountMajorUnits(totals, "total")
                : 0m;
            var created = obj.TryGetProperty("billed_at", out var billedAt) && billedAt.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(billedAt.GetString(), out var billedAtValue)
                ? billedAtValue
                : obj.TryGetProperty("created_at", out var createdAt) && DateTimeOffset.TryParse(createdAt.GetString(), out var createdAtValue) ? createdAtValue : DateTimeOffset.UtcNow;
            var isPaid = status is "completed" or "paid";

            string? invoicePdfUrl = null;
            if (status is "completed" or "billed")
            {
                try
                {
                    var invoiceJson = await GetJsonAsync($"transactions/{Uri.EscapeDataString(id)}/invoice", cancellationToken);
                    invoicePdfUrl = invoiceJson.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                }
                catch (PaymentGatewayException)
                {
                    // Best-effort - a transaction without an invoice PDF yet (e.g. still processing) must
                    // not block the rest of the payment history from showing.
                }
            }

            invoices.Add(new PaymentInvoice(
                id,
                obj.TryGetProperty("invoice_number", out var numberProp) ? numberProp.GetString() : null,
                isPaid ? total : 0m,
                total,
                currency,
                status,
                created,
                null,
                invoicePdfUrl));
        }

        return invoices;
    }

    public PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader)
    {
        VerifySignature(payload, signatureHeader);
        try
        {
            using var doc = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
            var root = doc.RootElement;
            var eventId = RequiredString(root, "notification_id");
            var type = RequiredString(root, "event_type");
            if (!Handled.Contains(type)) return null;

            var occurredAt = RequiredDateTime(root, "occurred_at");
            var data = root.GetProperty("data");
            var customer = RequiredString(data, "customer_id");
            var subscriptionId = RequiredString(data, "id");
            var status = MapStatus(type, data.TryGetProperty("status", out var statusProperty) ? statusProperty.GetString() : null);
            var matchedPlanKey = MatchConfiguredPlan(data);
            if (status != SubscriptionStatus.Cancelled && matchedPlanKey is null) status = SubscriptionStatus.Unknown;

            var (periodStart, periodEnd) = ReadBillingPeriod(data, occurredAt);

            return new PaymentWebhookEvent(
                eventId,
                type,
                customer,
                subscriptionId,
                matchedPlanKey ?? SubscriptionPlan.Free.Key,
                status,
                occurredAt,
                periodStart,
                periodEnd,
                // Paddle never needs an organizationId shortcut the way Stripe's checkout metadata did -
                // there's no separate "trust this field" step at all, routing is purely by PaddleCustomerId,
                // which SubscriptionService already treats as the fallback/authoritative path.
                null);
        }
        catch (PaymentWebhookValidationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentOutOfRangeException or KeyNotFoundException or InvalidOperationException)
        {
            throw new PaymentWebhookValidationException("Malformed Paddle webhook payload.", ex);
        }
    }

    public async Task<PaymentSubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId)) return null;

        var json = await GetJsonAsync($"subscriptions/{Uri.EscapeDataString(subscriptionId)}", cancellationToken);
        return MapSubscriptionState(json);
    }

    /// <summary>
    /// Discovers a customer's subscription without already knowing its Paddle subscription id - used to
    /// close the gap where a checkout completed but the created-subscription webhook never landed, so the
    /// org never got a PaddleSubscriptionId to reconcile by id in the first place.
    /// </summary>
    public async Task<PaymentSubscriptionState?> FindSubscriptionByCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return null;

        var json = await GetJsonAsync($"subscriptions?customer_id={Uri.EscapeDataString(customerId)}&per_page=1", cancellationToken);
        if (json.ValueKind != JsonValueKind.Array || json.GetArrayLength() == 0) return null;

        return MapSubscriptionState(json[0]);
    }

    public async Task<PlanChangeResult> ChangeSubscriptionPlanAsync(string subscriptionId, string newPlanKey, PlanChangeTiming timing, string idempotencyKey, CancellationToken cancellationToken, PromoCodeDiscount? discount = null)
    {
        if (!PlanPrices.TryGetValue(newPlanKey, out var newPriceId))
            throw new PaymentGatewayException($"Paddle:Prices:{newPlanKey} is not configured.");

        var body = await BuildChangeBodyAsync(newPriceId, timing, discount, cancellationToken);
        var updated = await PatchJsonAsync($"subscriptions/{Uri.EscapeDataString(subscriptionId)}", body, cancellationToken);

        // prevent_change (see BuildChangeBodyAsync) should make Paddle reject the whole update - the
        // subscription stays on its old price - when the immediate proration payment fails. Belt-and-braces:
        // if Paddle instead returned 200 with the old price still in place, treat it the same as a decline
        // rather than reporting success with nothing actually charged (audit: a correctly-charged upgrade
        // must never be indistinguishable from nothing happening, and neither must a silently-failed one).
        // TODO(paddle-sandbox): confirm against a real declined card whether Paddle actually returns a
        // non-2xx here (in which case SendJsonAsync's own PaymentGatewayException already covers it) or a
        // 200 with the update effectively reverted.
        if (timing == PlanChangeTiming.Immediately && MatchConfiguredPlan(updated) != newPlanKey)
            throw new PaymentGatewayException("Paddle did not apply the plan change (payment likely failed).", 402);

        var amountCharged = 0m;
        var currency = "EUR";
        DateTimeOffset? pendingEffectiveAt = null;

        if (timing == PlanChangeTiming.Immediately && updated.TryGetProperty("immediate_transaction", out var immediateTransaction) && immediateTransaction.ValueKind == JsonValueKind.Object)
        {
            (amountCharged, currency) = ReadTransactionTotal(immediateTransaction);
        }
        else if (timing == PlanChangeTiming.NextBillingPeriod && updated.TryGetProperty("scheduled_change", out var scheduledChange) && scheduledChange.ValueKind == JsonValueKind.Object)
        {
            pendingEffectiveAt = RequiredDateTime(scheduledChange, "effective_at");
        }

        return new PlanChangeResult(MapSubscriptionState(updated), amountCharged, currency, pendingEffectiveAt);
    }

    public async Task<PlanChangePreview> PreviewPlanChangeAsync(string subscriptionId, string newPlanKey, PlanChangeTiming timing, CancellationToken cancellationToken)
    {
        if (!PlanPrices.TryGetValue(newPlanKey, out var newPriceId))
            throw new PaymentGatewayException($"Paddle:Prices:{newPlanKey} is not configured.");

        var body = await BuildChangeBodyAsync(newPriceId, timing, null, cancellationToken);
        var preview = await PatchJsonAsync($"subscriptions/{Uri.EscapeDataString(subscriptionId)}/preview", body, cancellationToken);

        if (timing == PlanChangeTiming.Immediately && preview.TryGetProperty("immediate_transaction", out var immediateTransaction) && immediateTransaction.ValueKind == JsonValueKind.Object)
        {
            var (amount, currency) = ReadTransactionTotal(immediateTransaction);
            return new PlanChangePreview(amount, currency, null);
        }

        if (preview.TryGetProperty("scheduled_change", out var scheduledChange) && scheduledChange.ValueKind == JsonValueKind.Object)
        {
            return new PlanChangePreview(0m, "EUR", RequiredDateTime(scheduledChange, "effective_at"));
        }

        // No scheduled_change and no immediate_transaction: Paddle applied nothing to bill (e.g. same
        // price). Report the subscription's own current period end as the "nothing changes before" date.
        return new PlanChangePreview(0m, "EUR", RequiredDateTime(preview.TryGetProperty("current_billing_period", out var period) ? period : preview, "ends_at"));
    }

    public async Task CancelScheduledChangeAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        await PatchJsonAsync(
            $"subscriptions/{Uri.EscapeDataString(subscriptionId)}",
            new Dictionary<string, object?> { ["scheduled_change"] = null },
            cancellationToken);
    }

    private async Task<Dictionary<string, object?>> BuildChangeBodyAsync(string newPriceId, PlanChangeTiming timing, PromoCodeDiscount? discount, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["items"] = new[] { new Dictionary<string, object?> { ["price_id"] = newPriceId, ["quantity"] = 1 } },
            ["proration_billing_mode"] = timing == PlanChangeTiming.Immediately ? "prorated_immediately" : "full_next_billing_period",
            // Fail closed: if the immediate proration payment fails, the plan must not change - mirrors the
            // Stripe fix (error_if_incomplete) that closed a free-upgrade hole where a failed charge still
            // silently applied the higher plan.
            ["on_payment_failure"] = "prevent_change"
        };

        if (discount is not null)
        {
            var discountId = await EnsureDiscountAsync(discount, newPriceId, cancellationToken);
            body["discount"] = new Dictionary<string, object?> { ["id"] = discountId, ["effective_from"] = "immediately" };
        }

        return body;
    }

    private static (decimal Amount, string Currency) ReadTransactionTotal(JsonElement transaction)
    {
        var currency = transaction.TryGetProperty("currency_code", out var currencyProp)
            ? (currencyProp.GetString() ?? "EUR").ToUpperInvariant()
            : "EUR";
        if (!transaction.TryGetProperty("details", out var details) || !details.TryGetProperty("totals", out var totals))
            return (0m, currency);

        var grandTotal = ReadAmountMajorUnits(totals, "grand_total");
        var amount = grandTotal != 0m ? grandTotal : ReadAmountMajorUnits(totals, "total");
        return (amount, currency);
    }

    private static decimal ReadAmountMajorUnits(JsonElement totals, string property) =>
        totals.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var minorUnits)
            ? minorUnits / 100m
            : 0m;

    private PaymentSubscriptionState MapSubscriptionState(JsonElement obj)
    {
        var customer = RequiredString(obj, "customer_id");
        var status = MapStatus(string.Empty, obj.TryGetProperty("status", out var statusProperty) ? statusProperty.GetString() : null);
        var matchedPlanKey = MatchConfiguredPlan(obj);
        if (status != SubscriptionStatus.Cancelled && matchedPlanKey is null) status = SubscriptionStatus.Unknown;

        var (periodStart, periodEnd) = ReadBillingPeriod(obj, DateTimeOffset.UtcNow);

        return new PaymentSubscriptionState(
            customer,
            RequiredString(obj, "id"),
            matchedPlanKey ?? SubscriptionPlan.Free.Key,
            status,
            periodStart,
            periodEnd,
            null);
    }

    /// <summary>Finds the configured plan whose Paddle Price ID matches one of this subscription's line
    /// items. Returns null when no configured price matches - callers must treat that as "unknown plan",
    /// never fall back to trusting whatever Paddle sent (audyt: entitlement must never be inferred from
    /// unrecognized price data).</summary>
    private string? MatchConfiguredPlan(JsonElement obj)
    {
        if (!obj.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return null;

        var itemPriceIds = items.EnumerateArray()
            .Select(ReadItemPriceId)
            .Where(x => x is not null)
            .ToHashSet(StringComparer.Ordinal);

        var plans = PlanPrices;
        return plans.FirstOrDefault(kvp => itemPriceIds.Contains(kvp.Value)).Key;
    }

    private static string? ReadItemPriceId(JsonElement item)
    {
        if (item.TryGetProperty("price", out var price) && price.TryGetProperty("id", out var priceId) && priceId.ValueKind == JsonValueKind.String)
            return priceId.GetString();
        if (item.TryGetProperty("price_id", out var priceIdDirect) && priceIdDirect.ValueKind == JsonValueKind.String)
            return priceIdDirect.GetString();
        return null;
    }

    /// <summary>Paddle drops <c>current_billing_period</c> once a subscription is fully canceled - falls
    /// back to "now, one month" so callers always get usable (if meaningless for a cancelled sub) dates,
    /// mirroring how a cancelled Stripe subscription's periods were never load-bearing either.</summary>
    private static (DateTimeOffset Start, DateTimeOffset End) ReadBillingPeriod(JsonElement obj, DateTimeOffset fallback)
    {
        if (obj.TryGetProperty("current_billing_period", out var period) && period.ValueKind == JsonValueKind.Object
            && period.TryGetProperty("starts_at", out var startsAt) && DateTimeOffset.TryParse(startsAt.GetString(), out var start)
            && period.TryGetProperty("ends_at", out var endsAt) && DateTimeOffset.TryParse(endsAt.GetString(), out var end))
            return (start, end);

        return (fallback, fallback.AddMonths(1));
    }

    private SubscriptionStatus MapStatus(string eventType, string? status)
    {
        if (eventType == "subscription.canceled") return SubscriptionStatus.Cancelled;
        return status switch
        {
            "active" or "trialing" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" or "paused" => SubscriptionStatus.Cancelled,
            _ => Unknown(status)
        };
    }

    private SubscriptionStatus Unknown(string? status)
    {
        _logger.LogWarning("Unknown Paddle status {Status}; entitlement quarantined.", status);
        return SubscriptionStatus.Unknown;
    }

    /// <summary>Verifies the `Paddle-Signature: ts=...;h1=...` header: HMAC-SHA256 of "{ts}:{rawBody}" with
    /// the webhook secret, hex-encoded, compared in constant time - same shape as Stripe's
    /// `t=...,v1=...` scheme but with `;`/`:` separators instead of `,`/`.` (developer.paddle.com/webhooks/
    /// about/signature-verification).</summary>
    private void VerifySignature(string payload, string header)
    {
        if (string.IsNullOrWhiteSpace(WebhookSecret)) throw new PaymentGatewayException("Paddle:WebhookSecret is not configured.");
        if (string.IsNullOrWhiteSpace(header) || header.Length > 4096) throw new PaymentWebhookValidationException("Invalid Paddle signature header.");

        string? timestamp = null;
        string? signature = null;
        foreach (var part in header.Split(';'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "ts") timestamp = pair[1];
            else if (pair[0] == "h1") signature = pair[1];
        }

        if (!long.TryParse(timestamp, out var unix) || string.IsNullOrWhiteSpace(signature))
            throw new PaymentWebhookValidationException("Malformed Paddle signature header.");

        DateTimeOffset eventTime;
        try
        {
            eventTime = DateTimeOffset.FromUnixTimeSeconds(unix);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new PaymentWebhookValidationException("Malformed Paddle timestamp.", ex);
        }

        if ((DateTimeOffset.UtcNow - eventTime).Duration() > TimeSpan.FromMinutes(5))
            throw new PaymentWebhookValidationException("Expired Paddle signature.");

        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(WebhookSecret!),
            Encoding.UTF8.GetBytes($"{timestamp}:{payload}"));

        byte[] candidate;
        try
        {
            candidate = Convert.FromHexString(signature);
        }
        catch (FormatException ex)
        {
            throw new PaymentWebhookValidationException("Malformed Paddle signature.", ex);
        }

        if (candidate.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(candidate, expected))
            throw new PaymentWebhookValidationException("Invalid Paddle signature.");
    }

    private Task<JsonElement> PostJsonAsync(string path, Dictionary<string, object?> body, CancellationToken cancellationToken) =>
        SendJsonAsync(HttpMethod.Post, path, body, cancellationToken);

    private Task<JsonElement> PatchJsonAsync(string path, Dictionary<string, object?> body, CancellationToken cancellationToken) =>
        SendJsonAsync(new HttpMethod("PATCH"), path, body, cancellationToken);

    private async Task<JsonElement> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await SendAsync(request, path, cancellationToken);
    }

    private async Task<JsonElement> SendJsonAsync(HttpMethod method, string path, Dictionary<string, object?> body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        return await SendAsync(request, path, cancellationToken);
    }

    /// <summary>Sends the request and returns the unwrapped `data` payload - every Paddle API response is
    /// shaped as <c>{"data": ..., "meta": {...}}</c> (or <c>{"error": {...}}</c> on failure).</summary>
    private async Task<JsonElement> SendAsync(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) throw new PaymentGatewayException("Paddle:ApiKey is not configured.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException("Paddle transport failure.", ex);
        }

        using (response)
        {
            var body = await ReadLimitedAsync(response.Content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Paddle error bodies are validation detail (field/message), never secrets - safe to log
                // in full, and worth it: a bare status code alone already cost one debugging round trip
                // for a rejected request field.
                _logger.LogError("Paddle API {Status} at {Path}: {Body}", response.StatusCode, path.Split('?')[0], body);
                throw new PaymentGatewayException($"Paddle API error {(int)response.StatusCode}", (int)response.StatusCode);
            }

            try
            {
                using var doc = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
                return doc.RootElement.TryGetProperty("data", out var data) ? data.Clone() : doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new PaymentGatewayException("Paddle returned invalid JSON.", ex);
            }
        }
    }

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxResponseBytes) throw new PaymentGatewayException("Paddle response too large.");

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[16_384];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (output.Length + read > MaxResponseBytes) throw new PaymentGatewayException("Paddle response too large.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string RequiredString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new JsonException($"Missing {property}");

    private static DateTimeOffset RequiredDateTime(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : throw new JsonException($"Missing {property}");
}
