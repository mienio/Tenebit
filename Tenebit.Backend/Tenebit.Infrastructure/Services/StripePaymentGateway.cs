using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Infrastructure.Services;

public sealed class StripePaymentGateway : IPaymentGateway
{
    private const int MaxResponseBytes = 1024 * 1024;
    private static readonly HashSet<string> Handled = new(StringComparer.Ordinal)
    {
        "customer.subscription.created",
        "customer.subscription.updated",
        "customer.subscription.deleted"
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(HttpClient http, IConfiguration configuration, ILogger<StripePaymentGateway> logger)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://api.stripe.com/v1/");
        _http.Timeout = TimeSpan.FromSeconds(15);
        _configuration = configuration;
        _logger = logger;
    }

    private string? SecretKey => _configuration["Stripe:SecretKey"];
    private string? WebhookSecret => _configuration["Stripe:WebhookSecret"];

    // One Stripe Price per paid plan, configured under Stripe:Prices:<planKey> (e.g. Stripe:Prices:business).
    // The Free plan never has a price - it's never looked up here.
    private IReadOnlyDictionary<string, string> PlanPrices => _configuration.GetSection("Stripe:Prices")
        .GetChildren()
        .Where(x => !string.IsNullOrWhiteSpace(x.Value))
        .ToDictionary(x => x.Key, x => x.Value!, StringComparer.Ordinal);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) &&
        !string.IsNullOrWhiteSpace(WebhookSecret) &&
        PlanPrices.Count > 0;

    public bool IsPlanConfigured(string planKey) => PlanPrices.ContainsKey(planKey);

    public async Task<string> CreateCustomerAsync(string email, Guid organizationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var json = await PostAsync(
            "customers",
            new Dictionary<string, string>
            {
                ["email"] = email,
                ["metadata[organizationId]"] = organizationId.ToString()
            },
            idempotencyKey,
            cancellationToken);
        return RequiredString(json, "id");
    }

    public async Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string planKey, string successUrl, string cancelUrl, string idempotencyKey, CancellationToken cancellationToken, PromoCodeDiscount? discount = null)
    {
        if (!PlanPrices.TryGetValue(planKey, out var priceId))
            throw new PaymentGatewayException($"Stripe:Prices:{planKey} is not configured.");

        var form = new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["customer"] = customerId,
            ["client_reference_id"] = organizationId.ToString(),
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["line_items[0][price]"] = priceId,
            ["line_items[0][quantity]"] = "1",
            ["subscription_data[metadata][organizationId]"] = organizationId.ToString()
        };

        if (discount is not null)
        {
            var couponId = await EnsureCouponAsync(discount, cancellationToken);
            form["discounts[0][coupon]"] = couponId;
        }

        var json = await PostAsync("checkout/sessions", form, idempotencyKey, cancellationToken);
        return RequiredString(json, "url");
    }

    /// <summary>
    /// Our promo codes are our own marketing entities, not Stripe objects - Stripe Checkout Sessions can
    /// only reference an existing Coupon. This creates one on first use (idempotency key derived from the
    /// discount shape, so repeated checkouts with the same code reuse the same coupon instead of piling
    /// up duplicates) applied to the first invoice only ("once"), matching how a promo code is understood
    /// everywhere else in the product: a discount on the signup, not a permanent price change.
    /// </summary>
    private async Task<string> EnsureCouponAsync(PromoCodeDiscount discount, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string> { ["duration"] = "once" };
        string idempotencyKey;
        if (discount.Type == PromoDiscountType.Percentage)
        {
            form["percent_off"] = discount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            idempotencyKey = $"tenebit-coupon-pct-{discount.Value:0.##}";
        }
        else
        {
            form["amount_off"] = ((long)Math.Round(discount.Value * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            form["currency"] = "eur";
            idempotencyKey = $"tenebit-coupon-amt-{discount.Value:0.##}";
        }

        var json = await PostAsync("coupons", form, idempotencyKey, cancellationToken);
        return RequiredString(json, "id");
    }

    public async Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken)
    {
        var json = await PostAsync(
            "billing_portal/sessions",
            new Dictionary<string, string>
            {
                ["customer"] = customerId,
                ["return_url"] = returnUrl
            },
            null,
            cancellationToken);
        return RequiredString(json, "url");
    }

    public PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader)
    {
        VerifySignature(payload, signatureHeader);
        try
        {
            using var doc = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
            var root = doc.RootElement;
            var eventId = RequiredString(root, "id");
            var type = RequiredString(root, "type");
            if (!Handled.Contains(type)) return null;

            var created = RequiredUnix(root, "created");
            var obj = root.GetProperty("data").GetProperty("object");
            var customer = RequiredString(obj, "customer");
            var subscription = RequiredString(obj, "id");
            var status = MapStatus(type, obj.TryGetProperty("status", out var statusProperty) ? statusProperty.GetString() : null);
            var matchedPlanKey = MatchConfiguredPlan(obj);
            if (status != SubscriptionStatus.Cancelled && matchedPlanKey is null) status = SubscriptionStatus.Unknown;

            return new PaymentWebhookEvent(
                eventId,
                type,
                customer,
                subscription,
                matchedPlanKey ?? SubscriptionPlan.Free.Key,
                status,
                created,
                RequiredPeriodUnix(obj, "current_period_start"),
                RequiredPeriodUnix(obj, "current_period_end"),
                ReadOrganizationId(obj));
        }
        catch (PaymentWebhookValidationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentOutOfRangeException)
        {
            throw new PaymentWebhookValidationException("Malformed Stripe webhook payload.", ex);
        }
    }

    public async Task<PaymentSubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId)) return null;

        var obj = await GetAsync($"subscriptions/{Uri.EscapeDataString(subscriptionId)}?expand[]=items.data.price", cancellationToken);
        return MapSubscriptionState(obj);
    }

    /// <summary>
    /// Discovers a customer's subscription without already knowing its Stripe subscription id - used to
    /// close the gap where a checkout completed but the created-subscription webhook never landed (lost
    /// delivery, or arrived before this endpoint's signing secret was corrected), so the org never got a
    /// StripeSubscriptionId to reconcile by id in the first place.
    /// </summary>
    public async Task<PaymentSubscriptionState?> FindSubscriptionByCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return null;

        var json = await GetAsync(
            $"subscriptions?customer={Uri.EscapeDataString(customerId)}&status=all&limit=1&expand[]=data.items.data.price",
            cancellationToken);
        if (!json.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            return null;

        return MapSubscriptionState(data[0]);
    }

    /// <summary>
    /// Switches an existing live subscription directly to a different configured plan - both upgrade and
    /// downgrade - via Stripe's own subscription-item price swap with automatic proration, instead of
    /// routing every plan change through Checkout/Billing Portal. Checkout only ever creates the first
    /// subscription (<see cref="CreateCheckoutSessionAsync"/> refuses a second one on purpose); this is
    /// the path for changing an already-live one.
    /// </summary>
    public async Task<PaymentSubscriptionState> ChangeSubscriptionPlanAsync(string subscriptionId, string newPlanKey, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!PlanPrices.TryGetValue(newPlanKey, out var newPriceId))
            throw new PaymentGatewayException($"Stripe:Prices:{newPlanKey} is not configured.");

        var current = await GetAsync($"subscriptions/{Uri.EscapeDataString(subscriptionId)}?expand[]=items.data.price", cancellationToken);
        if (!current.TryGetProperty("items", out var items) ||
            !items.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() == 0)
            throw new PaymentGatewayException("Stripe subscription has no item to switch.");
        var itemId = RequiredString(data[0], "id");

        // create_prorations (the Stripe default) only invoices immediately when the billing interval
        // changes or the customer moves from free to paid - a same-interval plan swap just books the
        // proration against the *next* renewal invoice and, with the also-default payment_behavior of
        // allow_incomplete, applies the new (possibly much higher) plan right away regardless of whether
        // that eventual charge ever succeeds. That combination let an org switch to any configured plan
        // for free. always_invoice forces an invoice for the proration now, and error_if_incomplete makes
        // Stripe reject the whole update - the plan stays unchanged - unless that invoice is actually paid.
        //
        // billing_cycle_anchor=now resets the renewal date to the moment of the switch instead of leaving
        // it at the old plan's period end (Stripe's default). Combined with always_invoice this bills one
        // full period of the new plan right away, credited for the unused time left on the old one - the
        // same shape customers already know from Claude.ai's own Pro-to-Max upgrade - rather than a partial
        // top-up that leaves the org on the new plan's entitlements for whatever days happened to be left
        // on the old billing cycle.
        var form = new Dictionary<string, string>
        {
            ["items[0][id]"] = itemId,
            ["items[0][price]"] = newPriceId,
            ["proration_behavior"] = "always_invoice",
            ["payment_behavior"] = "error_if_incomplete",
            ["billing_cycle_anchor"] = "now",
            ["expand[0]"] = "items.data.price"
        };

        var updated = await PostAsync($"subscriptions/{Uri.EscapeDataString(subscriptionId)}", form, idempotencyKey, cancellationToken);
        return MapSubscriptionState(updated);
    }

    /// <summary>
    /// Downgrades must not take effect (or credit anything) until the current period actually ends, so
    /// this can't reuse ChangeSubscriptionPlanAsync's immediate price swap. Follows Stripe's documented
    /// recipe for scheduling a plan change on an existing subscription (subscription-schedules#changing-
    /// subscriptions): attach a schedule that mirrors the subscription's current phase unchanged, then add
    /// a second phase - the new plan, starting exactly when the first one ends - and let end_behavior=release
    /// hand the subscription back to normal (unscheduled) billing once that phase begins.
    /// </summary>
    public async Task<PaymentScheduleState> ScheduleDowngradeAsync(string subscriptionId, string? existingScheduleId, string newPlanKey, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!PlanPrices.TryGetValue(newPlanKey, out var newPriceId))
            throw new PaymentGatewayException($"Stripe:Prices:{newPlanKey} is not configured.");

        JsonElement schedule;
        if (string.IsNullOrWhiteSpace(existingScheduleId))
        {
            schedule = await PostAsync("subscription_schedules", new Dictionary<string, string> { ["from_subscription"] = subscriptionId }, $"{idempotencyKey}-create", cancellationToken);
        }
        else
        {
            try
            {
                schedule = await GetAsync($"subscription_schedules/{Uri.EscapeDataString(existingScheduleId)}", cancellationToken);
            }
            catch (PaymentGatewayException ex) when (ex.StatusCode == 404)
            {
                // The previously scheduled change already ran its course (or was released on Stripe's side
                // some other way) since we last saw it - start a fresh schedule instead of failing outright.
                schedule = await PostAsync("subscription_schedules", new Dictionary<string, string> { ["from_subscription"] = subscriptionId }, $"{idempotencyKey}-create", cancellationToken);
            }
        }

        var scheduleId = RequiredString(schedule, "id");
        if (!schedule.TryGetProperty("phases", out var phases) || phases.ValueKind != JsonValueKind.Array || phases.GetArrayLength() == 0)
            throw new PaymentGatewayException("Stripe subscription schedule has no current phase.");
        var phase0 = phases[0];
        if (!phase0.TryGetProperty("items", out var phase0Items) || phase0Items.ValueKind != JsonValueKind.Array || phase0Items.GetArrayLength() == 0)
            throw new PaymentGatewayException("Stripe subscription schedule phase has no item.");
        var phase0Item = phase0Items[0];
        var phase0PriceId = phase0Item.TryGetProperty("price", out var priceElement)
            ? priceElement.ValueKind == JsonValueKind.String ? priceElement.GetString()! : RequiredString(priceElement, "id")
            : throw new PaymentGatewayException("Stripe subscription schedule phase item has no price.");
        var phase0Quantity = phase0Item.TryGetProperty("quantity", out var quantityElement) && quantityElement.TryGetInt32(out var quantity) ? quantity : 1;
        var phase0Start = RequiredUnix(phase0, "start_date");
        var phase0End = RequiredUnix(phase0, "end_date");

        var form = new Dictionary<string, string>
        {
            ["phases[0][items][0][price]"] = phase0PriceId,
            ["phases[0][items][0][quantity]"] = phase0Quantity.ToString(),
            ["phases[0][start_date]"] = phase0Start.ToUnixTimeSeconds().ToString(),
            ["phases[0][end_date]"] = phase0End.ToUnixTimeSeconds().ToString(),
            ["phases[1][items][0][price]"] = newPriceId,
            ["phases[1][duration][interval]"] = "month",
            ["phases[1][duration][interval_count]"] = "1",
            ["end_behavior"] = "release"
        };

        await PostAsync($"subscription_schedules/{Uri.EscapeDataString(scheduleId)}", form, idempotencyKey, cancellationToken);
        return new PaymentScheduleState(scheduleId, newPlanKey, phase0End);
    }

    public async Task ReleaseScheduleAsync(string scheduleId, CancellationToken cancellationToken)
    {
        await PostAsync($"subscription_schedules/{Uri.EscapeDataString(scheduleId)}/release", new Dictionary<string, string>(), null, cancellationToken);
    }

    private PaymentSubscriptionState MapSubscriptionState(JsonElement obj)
    {
        var customer = RequiredString(obj, "customer");
        var status = MapStatus(string.Empty, obj.TryGetProperty("status", out var statusProperty) ? statusProperty.GetString() : null);
        var matchedPlanKey = MatchConfiguredPlan(obj);
        if (status != SubscriptionStatus.Cancelled && matchedPlanKey is null) status = SubscriptionStatus.Unknown;

        return new PaymentSubscriptionState(
            customer,
            RequiredString(obj, "id"),
            matchedPlanKey ?? SubscriptionPlan.Free.Key,
            status,
            RequiredPeriodUnix(obj, "current_period_start"),
            RequiredPeriodUnix(obj, "current_period_end"),
            ReadOrganizationId(obj));
    }

    /// <summary>Finds the configured plan whose Stripe Price ID matches one of this subscription's line
    /// items. Returns null when no configured price matches - callers must treat that as "unknown plan",
    /// never fall back to trusting whatever Stripe sent (audyt: entitlement must never be inferred from
    /// unrecognized price data).</summary>
    private string? MatchConfiguredPlan(JsonElement obj)
    {
        if (!obj.TryGetProperty("items", out var items) ||
            !items.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array) return null;

        var itemPriceIds = data.EnumerateArray()
            .Where(x => x.TryGetProperty("price", out var price) && price.TryGetProperty("id", out _))
            .Select(x => x.GetProperty("price").GetProperty("id").GetString())
            .ToHashSet(StringComparer.Ordinal);

        var plans = PlanPrices;
        return plans.FirstOrDefault(kvp => itemPriceIds.Contains(kvp.Value)).Key;
    }

    private SubscriptionStatus MapStatus(string eventType, string? status)
    {
        if (eventType == "customer.subscription.deleted") return SubscriptionStatus.Cancelled;
        return status switch
        {
            "active" or "trialing" => SubscriptionStatus.Active,
            "past_due" or "incomplete" => SubscriptionStatus.PastDue,
            "canceled" or "unpaid" or "incomplete_expired" => SubscriptionStatus.Cancelled,
            _ => Unknown(status)
        };
    }

    private SubscriptionStatus Unknown(string? status)
    {
        _logger.LogWarning("Unknown Stripe status {Status}; entitlement quarantined.", status);
        return SubscriptionStatus.Unknown;
    }

    private void VerifySignature(string payload, string header)
    {
        if (string.IsNullOrWhiteSpace(WebhookSecret)) throw new PaymentGatewayException("Stripe:WebhookSecret is not configured.");
        if (string.IsNullOrWhiteSpace(header) || header.Length > 4096) throw new PaymentWebhookValidationException("Invalid Stripe signature header.");

        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in header.Split(','))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "t") timestamp = pair[1];
            else if (pair[0] == "v1") signatures.Add(pair[1]);
        }

        if (!long.TryParse(timestamp, out var unix) || signatures.Count == 0)
            throw new PaymentWebhookValidationException("Malformed Stripe signature header.");

        DateTimeOffset eventTime;
        try
        {
            eventTime = DateTimeOffset.FromUnixTimeSeconds(unix);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new PaymentWebhookValidationException("Malformed Stripe timestamp.", ex);
        }

        if ((DateTimeOffset.UtcNow - eventTime).Duration() > TimeSpan.FromMinutes(5))
            throw new PaymentWebhookValidationException("Expired Stripe signature.");

        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(WebhookSecret!),
            Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));

        foreach (var signature in signatures)
        {
            try
            {
                var candidate = Convert.FromHexString(signature);
                if (candidate.Length == expected.Length && CryptographicOperations.FixedTimeEquals(candidate, expected)) return;
            }
            catch (FormatException)
            {
                // Ignore malformed v1 entries and continue checking the remaining signatures.
            }
        }

        throw new PaymentWebhookValidationException("Invalid Stripe signature.");
    }

    private async Task<JsonElement> PostAsync(string path, Dictionary<string, string> form, string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await SendAsync(request, path, cancellationToken);
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await SendAsync(request, path, cancellationToken);
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SecretKey)) throw new PaymentGatewayException("Stripe:SecretKey is not configured.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException("Stripe transport failure.", ex);
        }

        using (response)
        {
            var body = await ReadLimitedAsync(response.Content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                response.Headers.TryGetValues("Request-Id", out var requestIds);
                _logger.LogError(
                    "Stripe API {Status} at {Path}; requestId={RequestId}; bytes={Bytes}",
                    response.StatusCode,
                    path.Split('?')[0],
                    requestIds?.FirstOrDefault() ?? "unknown",
                    Encoding.UTF8.GetByteCount(body));
                throw new PaymentGatewayException($"Stripe API error {(int)response.StatusCode}", (int)response.StatusCode);
            }

            try
            {
                using var doc = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
                return doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new PaymentGatewayException("Stripe returned invalid JSON.", ex);
            }
        }
    }

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxResponseBytes) throw new PaymentGatewayException("Stripe response too large.");

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[16_384];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (output.Length + read > MaxResponseBytes) throw new PaymentGatewayException("Stripe response too large.");
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

    private static DateTimeOffset RequiredUnix(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var unix)
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : throw new JsonException($"Missing {property}");

    /// <summary>Stripe API versions from 2025-03-31 onward dropped current_period_start/current_period_end
    /// from the Subscription object itself - the same fields now live only on its first SubscriptionItem.
    /// Checking the root first keeps this working against older pinned API versions/test fixtures too.</summary>
    private static DateTimeOffset RequiredPeriodUnix(JsonElement obj, string property)
    {
        if (obj.TryGetProperty(property, out var rootValue) && rootValue.TryGetInt64(out var rootUnix))
            return DateTimeOffset.FromUnixTimeSeconds(rootUnix);

        if (obj.TryGetProperty("items", out var items) &&
            items.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array &&
            data.GetArrayLength() > 0 &&
            data[0].TryGetProperty(property, out var itemValue) &&
            itemValue.TryGetInt64(out var itemUnix))
            return DateTimeOffset.FromUnixTimeSeconds(itemUnix);

        throw new JsonException($"Missing {property}");
    }

    private static Guid? ReadOrganizationId(JsonElement element)
    {
        if (!element.TryGetProperty("metadata", out var metadata) ||
            !metadata.TryGetProperty("organizationId", out var organizationId) ||
            string.IsNullOrWhiteSpace(organizationId.GetString())) return null;

        if (Guid.TryParse(organizationId.GetString(), out var id)) return id;
        throw new PaymentWebhookValidationException("Invalid organization metadata.");
    }
}
