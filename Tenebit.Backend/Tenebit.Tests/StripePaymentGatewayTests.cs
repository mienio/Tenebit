using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Subscriptions;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class StripePaymentGatewayTests
{
    /// <summary>Records every request it sees and replays canned responses keyed by HTTP method + path
    /// prefix, in the order given - just enough to drive <see cref="StripePaymentGateway"/> without a
    /// real Stripe account.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpMethod Method, string PathPrefix, HttpStatusCode Status, string Body)> _responses = new();
        public readonly List<(HttpMethod Method, string Path, string? Body)> Requests = new();

        public StubHandler Enqueue(HttpMethod method, string pathPrefix, HttpStatusCode status, string body)
        {
            _responses.Enqueue((method, pathPrefix, status, body));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));

            if (_responses.Count == 0) throw new InvalidOperationException("No stubbed response left for " + request.RequestUri);
            var (method, prefix, status, respBody) = _responses.Dequeue();
            if (request.Method != method || !request.RequestUri!.AbsolutePath.Contains(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri.AbsolutePath}, expected {method} containing {prefix}");

            return new HttpResponseMessage(status) { Content = new StringContent(respBody, Encoding.UTF8, "application/json") };
        }
    }

    private static StripePaymentGateway CreateGateway(StubHandler handler, params (string PlanKey, string PriceId)[] prices)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = "sk_test_fake",
            ["Stripe:WebhookSecret"] = "whsec_test"
        };
        foreach (var (planKey, priceId) in prices) settings[$"Stripe:Prices:{planKey}"] = priceId;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://fake-stripe.test/v1/") };
        return new StripePaymentGateway(http, configuration, NullLogger<StripePaymentGateway>.Instance);
    }

    private const string CurrentSubscriptionJson = """
        {
          "id": "sub_1", "object": "subscription", "customer": "cus_1", "status": "active",
          "metadata": {},
          "items": { "data": [ { "id": "si_1", "price": { "id": "price_old" } } ] },
          "current_period_start": 1700000000, "current_period_end": 1702592000
        }
        """;

    [Fact]
    public async Task ChangeSubscriptionPlanAsync_RequestsImmediateInvoiceAndFailsClosedOnDecline()
    {
        // The critical fix (audit: free plan upgrades): a same-interval plan swap must invoice for the
        // proration right away (always_invoice) and must reject the whole update - not just mark the
        // subscription past_due - if that invoice can't be paid (error_if_incomplete). Without both
        // parameters Stripe applies the higher plan immediately regardless of payment outcome.
        var handler = new StubHandler()
            .Enqueue(HttpMethod.Get, "subscriptions/sub_1", HttpStatusCode.OK, CurrentSubscriptionJson)
            .Enqueue(HttpMethod.Post, "subscriptions/sub_1", HttpStatusCode.OK, CurrentSubscriptionJson);
        var gateway = CreateGateway(handler, ("growth", "price_growth"));

        await gateway.ChangeSubscriptionPlanAsync("sub_1", "growth", "idem-1", CancellationToken.None);

        var update = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.Contains("proration_behavior=always_invoice", update.Body);
        Assert.Contains("payment_behavior=error_if_incomplete", update.Body);
        Assert.Contains("items%5B0%5D%5Bprice%5D=price_growth", update.Body);
    }

    [Fact]
    public async Task ChangeSubscriptionPlanAsync_Throws402_WhenStripeDeclinesTheProrationInvoice()
    {
        var handler = new StubHandler()
            .Enqueue(HttpMethod.Get, "subscriptions/sub_1", HttpStatusCode.OK, CurrentSubscriptionJson)
            .Enqueue(HttpMethod.Post, "subscriptions/sub_1", HttpStatusCode.PaymentRequired, """{"error":{"message":"Your card was declined."}}""");
        var gateway = CreateGateway(handler, ("growth", "price_growth"));

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => gateway.ChangeSubscriptionPlanAsync("sub_1", "growth", "idem-1", CancellationToken.None));

        Assert.Equal(402, ex.StatusCode);
    }

    [Fact]
    public void ParseWebhookEvent_SubscriptionOnDifferentPrice_IsQuarantinedWithoutProEntitlement()
    {
        const string secret = "whsec_test_secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = "sk_test_fake",
            ["Stripe:WebhookSecret"] = secret,
            ["Stripe:Prices:business"] = "price_pro_expected"
        }).Build();
        using var http = new HttpClient();
        var gateway = new StripePaymentGateway(http, configuration, NullLogger<StripePaymentGateway>.Instance);
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_other_price",
            type = "customer.subscription.updated",
            created = now.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = "sub_123",
                    customer = "cus_123",
                    status = "active",
                    current_period_start = now.ToUnixTimeSeconds(),
                    current_period_end = now.AddMonths(1).ToUnixTimeSeconds(),
                    metadata = new { },
                    items = new { data = new[] { new { price = new { id = "price_not_tenebit_pro" } } } }
                }
            }
        });
        var signed = $"{timestamp}.{payload}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();

        var parsed = gateway.ParseWebhookEvent(payload, $"t={timestamp},v1={signature}");

        Assert.NotNull(parsed);
        Assert.Equal(SubscriptionPlan.Free.Key, parsed!.PlanKey);
        Assert.Equal(SubscriptionStatus.Unknown, parsed.Status);
    }

    [Fact]
    public void ParseWebhookEvent_ReadsPeriodFromSubscriptionItem_WhenAbsentFromSubscriptionRoot()
    {
        // Stripe API versions from 2025-03-31 onward drop current_period_start/end from the Subscription
        // object itself - real webhook payloads on such an account only carry them on the first item.
        const string secret = "whsec_test_secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = "sk_test_fake",
            ["Stripe:WebhookSecret"] = secret,
            ["Stripe:Prices:business"] = "price_pro_expected"
        }).Build();
        using var http = new HttpClient();
        var gateway = new StripePaymentGateway(http, configuration, NullLogger<StripePaymentGateway>.Instance);
        var now = DateTimeOffset.UtcNow;
        var periodStart = now.ToUnixTimeSeconds();
        var periodEnd = now.AddMonths(1).ToUnixTimeSeconds();
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_item_period",
            type = "customer.subscription.created",
            created = now.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = "sub_123",
                    customer = "cus_123",
                    status = "active",
                    metadata = new { },
                    items = new
                    {
                        data = new[]
                        {
                            new
                            {
                                price = new { id = "price_pro_expected" },
                                current_period_start = periodStart,
                                current_period_end = periodEnd
                            }
                        }
                    }
                }
            }
        });
        var signed = $"{timestamp}.{payload}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();

        var parsed = gateway.ParseWebhookEvent(payload, $"t={timestamp},v1={signature}");

        Assert.NotNull(parsed);
        Assert.Equal(SubscriptionPlan.Business.Key, parsed!.PlanKey);
        Assert.Equal(SubscriptionStatus.Active, parsed.Status);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(periodStart), parsed.CurrentPeriodStart);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(periodEnd), parsed.CurrentPeriodEnd);
    }
}
