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

public class PaddlePaymentGatewayTests
{
    /// <summary>Records every request it sees and replays canned responses keyed by HTTP method + path
    /// prefix, in the order given - just enough to drive <see cref="PaddlePaymentGateway"/> without a
    /// real Paddle account.</summary>
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

    private static PaddlePaymentGateway CreateGateway(StubHandler handler, params (string PlanKey, string PriceId)[] prices)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Paddle:ApiKey"] = "fake_api_key",
            ["Paddle:ClientSideToken"] = "fake_client_token",
            ["Paddle:WebhookSecret"] = "whsec_test"
        };
        foreach (var (planKey, priceId) in prices) settings[$"Paddle:Prices:{planKey}"] = priceId;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://fake-paddle.test/") };
        return new PaddlePaymentGateway(http, configuration, NullLogger<PaddlePaymentGateway>.Instance);
    }

    private static string Wrap(string dataJson) => $$"""{"data": {{dataJson}}}""";

    private const string CurrentSubscriptionJson = """
        {
          "id": "sub_1", "customer_id": "ctm_1", "status": "active",
          "items": [ { "price": { "id": "pri_old" }, "quantity": 1 } ],
          "current_billing_period": { "starts_at": "2025-01-01T00:00:00Z", "ends_at": "2025-02-01T00:00:00Z" }
        }
        """;

    private const string ChangedToGrowthSubscriptionJson = """
        {
          "id": "sub_1", "customer_id": "ctm_1", "status": "active",
          "items": [ { "price": { "id": "pri_growth" }, "quantity": 1 } ],
          "current_billing_period": { "starts_at": "2025-01-15T00:00:00Z", "ends_at": "2025-02-15T00:00:00Z" },
          "immediate_transaction": {
            "currency_code": "EUR",
            "details": { "totals": { "total": "1700", "grand_total": "1700" } }
          }
        }
        """;

    [Fact]
    public async Task ChangeSubscriptionPlanAsync_Immediately_SendsProratedBillingModeAndFailClosedOnPaymentFailure()
    {
        // The critical fix (audit: free plan upgrades): an immediate plan switch must fail closed
        // (on_payment_failure=prevent_change) instead of silently applying the higher plan regardless of
        // whether the proration charge actually succeeds.
        var handler = new StubHandler()
            .Enqueue(HttpMethod.Patch, "subscriptions/sub_1", HttpStatusCode.OK, Wrap(ChangedToGrowthSubscriptionJson));
        var gateway = CreateGateway(handler, ("growth", "pri_growth"));

        var result = await gateway.ChangeSubscriptionPlanAsync("sub_1", "growth", PlanChangeTiming.Immediately, "idem-1", CancellationToken.None);

        Assert.Equal(17m, result.AmountCharged);
        Assert.Equal("EUR", result.Currency);

        var update = handler.Requests.Single();
        Assert.Contains("\"proration_billing_mode\":\"prorated_immediately\"", update.Body);
        Assert.Contains("\"on_payment_failure\":\"prevent_change\"", update.Body);
        Assert.Contains("\"price_id\":\"pri_growth\"", update.Body);
    }

    [Fact]
    public async Task ChangeSubscriptionPlanAsync_Throws402_WhenPaddleKeepsTheOldPriceAfterAFailedPayment()
    {
        // prevent_change means a declined proration charge leaves the subscription on its old price - this
        // must surface as a 402-shaped failure, not a silent no-op success (audit: a correctly-charged
        // upgrade must never be indistinguishable from a failed one).
        var handler = new StubHandler()
            .Enqueue(HttpMethod.Patch, "subscriptions/sub_1", HttpStatusCode.OK, Wrap(CurrentSubscriptionJson));
        var gateway = CreateGateway(handler, ("growth", "pri_growth"));

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => gateway.ChangeSubscriptionPlanAsync("sub_1", "growth", PlanChangeTiming.Immediately, "idem-1", CancellationToken.None));

        Assert.Equal(402, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeSubscriptionPlanAsync_Throws_WhenPaddleReturnsANonSuccessStatus()
    {
        var handler = new StubHandler()
            .Enqueue(HttpMethod.Patch, "subscriptions/sub_1", HttpStatusCode.PaymentRequired, """{"error":{"code":"transaction_payment_method_required"}}""");
        var gateway = CreateGateway(handler, ("growth", "pri_growth"));

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => gateway.ChangeSubscriptionPlanAsync("sub_1", "growth", PlanChangeTiming.Immediately, "idem-1", CancellationToken.None));

        Assert.Equal(402, ex.StatusCode);
    }

    private const string ScheduledDowngradeSubscriptionJson = """
        {
          "id": "sub_1", "customer_id": "ctm_1", "status": "active",
          "items": [ { "price": { "id": "pri_old" }, "quantity": 1 } ],
          "current_billing_period": { "starts_at": "2025-01-01T00:00:00Z", "ends_at": "2025-02-01T00:00:00Z" },
          "scheduled_change": { "action": "update", "effective_at": "2025-02-01T00:00:00Z" }
        }
        """;

    [Fact]
    public async Task ChangeSubscriptionPlanAsync_NextBillingPeriod_ChargesNothingAndReportsTheScheduledEffectiveDate()
    {
        var handler = new StubHandler()
            .Enqueue(HttpMethod.Patch, "subscriptions/sub_1", HttpStatusCode.OK, Wrap(ScheduledDowngradeSubscriptionJson));
        var gateway = CreateGateway(handler, ("starter", "pri_starter"));

        var result = await gateway.ChangeSubscriptionPlanAsync("sub_1", "starter", PlanChangeTiming.NextBillingPeriod, "idem-1", CancellationToken.None);

        Assert.Equal(0m, result.AmountCharged);
        Assert.Equal(new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero), result.PendingEffectiveAt);

        var update = handler.Requests.Single();
        Assert.Contains("\"proration_billing_mode\":\"full_next_billing_period\"", update.Body);
    }

    [Fact]
    public async Task CancelScheduledChangeAsync_SendsScheduledChangeNull()
    {
        var handler = new StubHandler().Enqueue(HttpMethod.Patch, "subscriptions/sub_1", HttpStatusCode.OK, Wrap(CurrentSubscriptionJson));
        var gateway = CreateGateway(handler);

        await gateway.CancelScheduledChangeAsync("sub_1", CancellationToken.None);

        Assert.Contains("\"scheduled_change\":null", handler.Requests.Single().Body);
    }

    private static string SignedHeader(string secret, string payload, long? timestampOverride = null)
    {
        var timestamp = (timestampOverride ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}:{payload}"))).ToLowerInvariant();
        return $"ts={timestamp};h1={signature}";
    }

    [Fact]
    public void ParseWebhookEvent_SubscriptionOnDifferentPrice_IsQuarantinedWithoutEntitlement()
    {
        const string secret = "whsec_test_secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paddle:ApiKey"] = "fake_api_key",
            ["Paddle:ClientSideToken"] = "fake_client_token",
            ["Paddle:WebhookSecret"] = secret,
            ["Paddle:Prices:business"] = "pri_business_expected"
        }).Build();
        using var http = new HttpClient();
        var gateway = new PaddlePaymentGateway(http, configuration, NullLogger<PaddlePaymentGateway>.Instance);
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            notification_id = "ntf_other_price",
            event_type = "subscription.updated",
            occurred_at = now.ToString("O"),
            data = new
            {
                id = "sub_123",
                customer_id = "ctm_123",
                status = "active",
                current_billing_period = new { starts_at = now.ToString("O"), ends_at = now.AddMonths(1).ToString("O") },
                items = new[] { new { price = new { id = "pri_not_tenebit" }, quantity = 1 } }
            }
        });

        var parsed = gateway.ParseWebhookEvent(payload, SignedHeader(secret, payload, now.ToUnixTimeSeconds()));

        Assert.NotNull(parsed);
        Assert.Equal(SubscriptionPlan.Free.Key, parsed!.PlanKey);
        Assert.Equal(SubscriptionStatus.Unknown, parsed.Status);
    }

    [Fact]
    public void ParseWebhookEvent_MatchesConfiguredPlan_AndReadsBillingPeriod()
    {
        const string secret = "whsec_test_secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paddle:ApiKey"] = "fake_api_key",
            ["Paddle:ClientSideToken"] = "fake_client_token",
            ["Paddle:WebhookSecret"] = secret,
            ["Paddle:Prices:business"] = "pri_business_expected"
        }).Build();
        using var http = new HttpClient();
        var gateway = new PaddlePaymentGateway(http, configuration, NullLogger<PaddlePaymentGateway>.Instance);
        var now = DateTimeOffset.UtcNow;
        var periodStart = now;
        var periodEnd = now.AddMonths(1);
        var payload = JsonSerializer.Serialize(new
        {
            notification_id = "ntf_item_period",
            event_type = "subscription.created",
            occurred_at = now.ToString("O"),
            data = new
            {
                id = "sub_123",
                customer_id = "ctm_123",
                status = "active",
                current_billing_period = new { starts_at = periodStart.ToString("O"), ends_at = periodEnd.ToString("O") },
                items = new[] { new { price = new { id = "pri_business_expected" }, quantity = 1 } }
            }
        });

        var parsed = gateway.ParseWebhookEvent(payload, SignedHeader(secret, payload, now.ToUnixTimeSeconds()));

        Assert.NotNull(parsed);
        Assert.Equal(SubscriptionPlan.Business.Key, parsed!.PlanKey);
        Assert.Equal(SubscriptionStatus.Active, parsed.Status);
        Assert.Equal(periodStart.ToUnixTimeSeconds(), parsed.CurrentPeriodStart.ToUnixTimeSeconds());
        Assert.Equal(periodEnd.ToUnixTimeSeconds(), parsed.CurrentPeriodEnd.ToUnixTimeSeconds());
    }

    [Fact]
    public void ParseWebhookEvent_RejectsATamperedPayload()
    {
        const string secret = "whsec_test_secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paddle:ApiKey"] = "fake_api_key",
            ["Paddle:ClientSideToken"] = "fake_client_token",
            ["Paddle:WebhookSecret"] = secret
        }).Build();
        using var http = new HttpClient();
        var gateway = new PaddlePaymentGateway(http, configuration, NullLogger<PaddlePaymentGateway>.Instance);
        var payload = "{\"event_type\":\"subscription.created\"}";
        var header = SignedHeader(secret, payload);

        Assert.Throws<PaymentWebhookValidationException>(() => gateway.ParseWebhookEvent(payload + "tampered", header));
    }

    private static PaddlePaymentGateway CreateWebhookOnlyGateway(string secret, out HttpClient http)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paddle:ApiKey"] = "fake_api_key",
            ["Paddle:ClientSideToken"] = "fake_client_token",
            ["Paddle:WebhookSecret"] = secret
        }).Build();
        http = new HttpClient();
        return new PaddlePaymentGateway(http, configuration, NullLogger<PaddlePaymentGateway>.Instance);
    }

    [Fact]
    public void ParseWebhookEvent_TransactionCompleted_ReadsAffiliateCodeAndNetEarnings()
    {
        const string secret = "whsec_test_secret";
        var gateway = CreateWebhookOnlyGateway(secret, out var http);
        using var _ = http;
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            notification_id = "ntf_txn_1",
            event_type = "transaction.completed",
            occurred_at = now.ToString("O"),
            data = new
            {
                id = "txn_abc",
                customer_id = "ctm_1",
                subscription_id = "sub_1",
                currency_code = "eur",
                origin = "web",
                billed_at = now.ToString("O"),
                custom_data = new { affiliate_code = "damian20" },
                details = new { totals = new { grand_total = "1700", fee = "150", earnings = "1550" } }
            }
        });

        var parsed = gateway.ParseWebhookEvent(payload, SignedHeader(secret, payload, now.ToUnixTimeSeconds()));

        Assert.NotNull(parsed);
        Assert.Equal("transaction.completed", parsed!.EventType);
        Assert.Equal("txn_abc", parsed.TransactionId);
        Assert.Equal("ctm_1", parsed.CustomerId);
        Assert.Equal("sub_1", parsed.SubscriptionId);
        // custom_data.affiliate_code arrives verbatim from Paddle.js customData - not re-normalized here
        // (AffiliateCodeRepository.GetByCodeAsync/AffiliateConversionRecordingService.RecordConversionAsync
        // does the case-insensitive lookup).
        Assert.Equal("damian20", parsed.AffiliateCode);
        Assert.Equal(17.00m, parsed.GrossAmount);
        Assert.Equal(15.50m, parsed.NetAmount);
        Assert.Equal("EUR", parsed.Currency);
        Assert.False(parsed.IsRenewal);
    }

    [Fact]
    public void ParseWebhookEvent_TransactionCompleted_NonWebOriginIsARenewal()
    {
        const string secret = "whsec_test_secret";
        var gateway = CreateWebhookOnlyGateway(secret, out var http);
        using var _ = http;
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            notification_id = "ntf_txn_2",
            event_type = "transaction.completed",
            occurred_at = now.ToString("O"),
            data = new
            {
                id = "txn_renewal",
                customer_id = "ctm_1",
                subscription_id = "sub_1",
                currency_code = "EUR",
                origin = "subscription_recurring",
                details = new { totals = new { grand_total = "1700", fee = "150", earnings = "1550" } }
            }
        });

        var parsed = gateway.ParseWebhookEvent(payload, SignedHeader(secret, payload, now.ToUnixTimeSeconds()));

        Assert.NotNull(parsed);
        Assert.True(parsed!.IsRenewal);
        Assert.Null(parsed.AffiliateCode);
    }

    [Fact]
    public void ParseWebhookEvent_TransactionCompleted_FallsBackToGrandTotalMinusFeeWhenEarningsMissing()
    {
        const string secret = "whsec_test_secret";
        var gateway = CreateWebhookOnlyGateway(secret, out var http);
        using var _ = http;
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            notification_id = "ntf_txn_3",
            event_type = "transaction.completed",
            occurred_at = now.ToString("O"),
            data = new
            {
                id = "txn_no_earnings",
                customer_id = "ctm_1",
                origin = "web",
                details = new { totals = new { grand_total = "1000", fee = "100" } }
            }
        });

        var parsed = gateway.ParseWebhookEvent(payload, SignedHeader(secret, payload, now.ToUnixTimeSeconds()));

        Assert.NotNull(parsed);
        Assert.Equal(10.00m, parsed!.GrossAmount);
        Assert.Equal(9.00m, parsed.NetAmount);
    }

    [Fact]
    public void ParseWebhookEvent_TransactionCompleted_DoesNotTouchSubscriptionEntitlementFields()
    {
        const string secret = "whsec_test_secret";
        var gateway = CreateWebhookOnlyGateway(secret, out var http);
        using var _ = http;
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            notification_id = "ntf_txn_4",
            event_type = "transaction.completed",
            occurred_at = now.ToString("O"),
            data = new
            {
                id = "txn_no_status",
                customer_id = "ctm_1",
                origin = "web",
                details = new { totals = new { grand_total = "1000" } }
            }
        });

        var parsed = gateway.ParseWebhookEvent(payload, SignedHeader(secret, payload, now.ToUnixTimeSeconds()));

        // Status/PlanKey are meaningless placeholders on a transaction event - SubscriptionService.HandleWebhookAsync
        // must branch away on EventType before ever reading these, never treat Unknown as "quarantine entitlement".
        Assert.NotNull(parsed);
        Assert.Equal(SubscriptionStatus.Unknown, parsed!.Status);
        Assert.Equal(SubscriptionPlan.Free.Key, parsed.PlanKey);
    }

    [Fact]
    public void ParseWebhookEvent_AdjustmentCreated_ReadsOriginalTransactionAndNetAmount()
    {
        const string secret = "whsec_test_secret";
        var gateway = CreateWebhookOnlyGateway(secret, out var http);
        using var _ = http;
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            notification_id = "ntf_adj_1",
            event_type = "adjustment.created",
            occurred_at = now.ToString("O"),
            data = new
            {
                id = "adj_1",
                transaction_id = "txn_abc",
                status = "approved",
                currency_code = "eur",
                totals = new { total = "1700", fee = "150", earnings = "1550" }
            }
        });

        var parsed = gateway.ParseWebhookEvent(payload, SignedHeader(secret, payload, now.ToUnixTimeSeconds()));

        Assert.NotNull(parsed);
        Assert.Equal("adjustment.created", parsed!.EventType);
        Assert.Equal("adj_1", parsed.TransactionId);
        Assert.Equal("txn_abc", parsed.OriginalTransactionId);
        Assert.Equal(17.00m, parsed.GrossAmount);
        Assert.Equal(15.50m, parsed.NetAmount);
        Assert.Equal("EUR", parsed.Currency);
    }

    [Fact]
    public void ParseWebhookEvent_AdjustmentCreated_IgnoresAnUnapprovedAdjustment()
    {
        const string secret = "whsec_test_secret";
        var gateway = CreateWebhookOnlyGateway(secret, out var http);
        using var _ = http;
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            notification_id = "ntf_adj_2",
            event_type = "adjustment.created",
            occurred_at = now.ToString("O"),
            data = new
            {
                id = "adj_2",
                transaction_id = "txn_abc",
                status = "pending_approval",
                totals = new { total = "1700", earnings = "1550" }
            }
        });

        var parsed = gateway.ParseWebhookEvent(payload, SignedHeader(secret, payload, now.ToUnixTimeSeconds()));

        Assert.Null(parsed);
    }
}
