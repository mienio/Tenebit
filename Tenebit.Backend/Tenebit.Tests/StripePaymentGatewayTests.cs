using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Domain.Subscriptions;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class StripePaymentGatewayTests
{
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
}
