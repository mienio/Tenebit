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
    private string? ProPriceId => _configuration["Stripe:ProPriceId"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) &&
        !string.IsNullOrWhiteSpace(WebhookSecret) &&
        !string.IsNullOrWhiteSpace(ProPriceId);

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

    public async Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string successUrl, string cancelUrl, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProPriceId)) throw new PaymentGatewayException("Stripe:ProPriceId is not configured.");

        var json = await PostAsync(
            "checkout/sessions",
            new Dictionary<string, string>
            {
                ["mode"] = "subscription",
                ["customer"] = customerId,
                ["client_reference_id"] = organizationId.ToString(),
                ["success_url"] = successUrl,
                ["cancel_url"] = cancelUrl,
                ["line_items[0][price]"] = ProPriceId,
                ["line_items[0][quantity]"] = "1",
                ["subscription_data[metadata][organizationId]"] = organizationId.ToString()
            },
            idempotencyKey,
            cancellationToken);
        return RequiredString(json, "url");
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
            var hasProPrice = HasConfiguredProPrice(obj);
            if (status != SubscriptionStatus.Cancelled && !hasProPrice) status = SubscriptionStatus.Unknown;

            return new PaymentWebhookEvent(
                eventId,
                type,
                customer,
                subscription,
                hasProPrice ? SubscriptionPlan.Pro.Key : SubscriptionPlan.Free.Key,
                status,
                created,
                RequiredUnix(obj, "current_period_start"),
                RequiredUnix(obj, "current_period_end"),
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
        var customer = RequiredString(obj, "customer");
        var status = MapStatus(string.Empty, obj.TryGetProperty("status", out var statusProperty) ? statusProperty.GetString() : null);
        var hasProPrice = HasConfiguredProPrice(obj);
        if (status != SubscriptionStatus.Cancelled && !hasProPrice) status = SubscriptionStatus.Unknown;

        return new PaymentSubscriptionState(
            customer,
            RequiredString(obj, "id"),
            hasProPrice ? SubscriptionPlan.Pro.Key : SubscriptionPlan.Free.Key,
            status,
            RequiredUnix(obj, "current_period_start"),
            RequiredUnix(obj, "current_period_end"),
            ReadOrganizationId(obj));
    }

    private bool HasConfiguredProPrice(JsonElement obj) =>
        !string.IsNullOrWhiteSpace(ProPriceId) &&
        obj.TryGetProperty("items", out var items) &&
        items.TryGetProperty("data", out var data) &&
        data.ValueKind == JsonValueKind.Array &&
        data.EnumerateArray().Any(x =>
            x.TryGetProperty("price", out var price) &&
            price.TryGetProperty("id", out var priceId) &&
            priceId.GetString() == ProPriceId);

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
                throw new PaymentGatewayException($"Stripe API error {(int)response.StatusCode}");
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

    private static Guid? ReadOrganizationId(JsonElement element)
    {
        if (!element.TryGetProperty("metadata", out var metadata) ||
            !metadata.TryGetProperty("organizationId", out var organizationId) ||
            string.IsNullOrWhiteSpace(organizationId.GetString())) return null;

        if (Guid.TryParse(organizationId.GetString(), out var id)) return id;
        throw new PaymentWebhookValidationException("Invalid organization metadata.");
    }
}
