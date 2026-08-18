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
        "customer.subscription.created", "customer.subscription.updated", "customer.subscription.deleted"
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
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(WebhookSecret) && !string.IsNullOrWhiteSpace(ProPriceId);

    public async Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken cancellationToken) =>
        RequiredString(await PostAsync("customers", new Dictionary<string,string>{{"email",email},{"metadata[organizationId]",organizationId.ToString()}}, cancellationToken), "id");

    public async Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProPriceId)) throw new PaymentGatewayException("Stripe:ProPriceId is not configured.");
        var json = await PostAsync("checkout/sessions", new Dictionary<string,string>
        {
            ["mode"]="subscription", ["customer"]=customerId, ["client_reference_id"]=organizationId.ToString(),
            ["success_url"]=successUrl, ["cancel_url"]=cancelUrl, ["line_items[0][price]"]=ProPriceId,
            ["line_items[0][quantity]"]="1", ["subscription_data[metadata][organizationId]"]=organizationId.ToString()
        }, cancellationToken);
        return RequiredString(json,"url");
    }

    public async Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken) =>
        RequiredString(await PostAsync("billing_portal/sessions", new Dictionary<string,string>{{"customer",customerId},{"return_url",returnUrl}}, cancellationToken), "url");

    public PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader)
    {
        VerifySignature(payload, signatureHeader);
        try
        {
            using var doc = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
            var root = doc.RootElement;
            var eventId = RequiredString(root,"id");
            var type = RequiredString(root,"type");
            if (!Handled.Contains(type)) return null;
            var created = RequiredUnix(root,"created");
            var obj = root.GetProperty("data").GetProperty("object");
            var customer = RequiredString(obj,"customer");
            var subscription = RequiredString(obj,"id");
            var status = MapStatus(type, obj.TryGetProperty("status", out var sp) ? sp.GetString() : null);
            return new PaymentWebhookEvent(eventId,type,customer,subscription,SubscriptionPlan.Pro.Key,status,created,
                RequiredUnix(obj,"current_period_start"),RequiredUnix(obj,"current_period_end"),ReadOrganizationId(obj));
        }
        catch (PaymentWebhookValidationException) { throw; }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentOutOfRangeException)
        { throw new PaymentWebhookValidationException("Malformed Stripe webhook payload.", ex); }
    }

    public async Task<PaymentSubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId)) return null;
        var obj = await GetAsync($"subscriptions/{Uri.EscapeDataString(subscriptionId)}?expand[]=items.data.price", cancellationToken);
        var id = RequiredString(obj,"id"); var customer = RequiredString(obj,"customer");
        var status = MapStatus(string.Empty, obj.TryGetProperty("status",out var sp) ? sp.GetString() : null);
        var hasProPrice = !string.IsNullOrWhiteSpace(ProPriceId) && obj.TryGetProperty("items",out var items) && items.TryGetProperty("data",out var data)
            && data.ValueKind == JsonValueKind.Array && data.EnumerateArray().Any(x => x.TryGetProperty("price",out var p) && p.TryGetProperty("id",out var pid) && pid.GetString()==ProPriceId);
        if (status != SubscriptionStatus.Cancelled && !hasProPrice) status = SubscriptionStatus.Unknown;
        return new PaymentSubscriptionState(customer,id,hasProPrice?SubscriptionPlan.Pro.Key:SubscriptionPlan.Free.Key,status,
            RequiredUnix(obj,"current_period_start"),RequiredUnix(obj,"current_period_end"),ReadOrganizationId(obj));
    }

    private SubscriptionStatus MapStatus(string eventType, string? status)
    {
        if (eventType == "customer.subscription.deleted") return SubscriptionStatus.Cancelled;
        return status switch { "active" or "trialing" => SubscriptionStatus.Active, "past_due" or "incomplete" => SubscriptionStatus.PastDue,
            "canceled" or "unpaid" or "incomplete_expired" => SubscriptionStatus.Cancelled, _ => Unknown(status) };
    }
    private SubscriptionStatus Unknown(string? status) { _logger.LogWarning("Unknown Stripe status {Status}; entitlement quarantined.",status); return SubscriptionStatus.Unknown; }

    private void VerifySignature(string payload, string header)
    {
        if (string.IsNullOrWhiteSpace(WebhookSecret)) throw new PaymentGatewayException("Stripe:WebhookSecret is not configured.");
        if (string.IsNullOrWhiteSpace(header) || header.Length > 4096) throw new PaymentWebhookValidationException("Invalid Stripe signature header.");
        string? ts=null; var sigs=new List<string>();
        foreach(var part in header.Split(',')){var pair=part.Split('=',2); if(pair.Length!=2) continue; if(pair[0]=="t") ts=pair[1]; else if(pair[0]=="v1") sigs.Add(pair[1]);}
        if(!long.TryParse(ts,out var unix) || sigs.Count==0) throw new PaymentWebhookValidationException("Malformed Stripe signature header.");
        DateTimeOffset eventTime; try { eventTime=DateTimeOffset.FromUnixTimeSeconds(unix); } catch(Exception ex){ throw new PaymentWebhookValidationException("Malformed Stripe timestamp.",ex); }
        if((DateTimeOffset.UtcNow-eventTime).Duration()>TimeSpan.FromMinutes(5)) throw new PaymentWebhookValidationException("Expired Stripe signature.");
        var expected=HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret!),Encoding.UTF8.GetBytes($"{ts}.{payload}"));
        foreach(var s in sigs){try{var candidate=Convert.FromHexString(s); if(candidate.Length==expected.Length && CryptographicOperations.FixedTimeEquals(candidate,expected)) return;}catch(FormatException){}}
        throw new PaymentWebhookValidationException("Invalid Stripe signature.");
    }

    private async Task<JsonElement> PostAsync(string path, Dictionary<string,string> form, CancellationToken ct)
    { using var req=new HttpRequestMessage(HttpMethod.Post,path){Content=new FormUrlEncodedContent(form)}; return await SendAsync(req,path,ct); }
    private async Task<JsonElement> GetAsync(string path, CancellationToken ct) { using var req=new HttpRequestMessage(HttpMethod.Get,path); return await SendAsync(req,path,ct); }
    private async Task<JsonElement> SendAsync(HttpRequestMessage req,string path,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(SecretKey)) throw new PaymentGatewayException("Stripe:SecretKey is not configured.");
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",SecretKey);
        HttpResponseMessage response; try { response=await _http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct); } catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException){ throw new PaymentGatewayException("Stripe transport failure.",ex); }
        using(response){var body=await ReadLimitedAsync(response.Content,ct); if(!response.IsSuccessStatusCode){response.Headers.TryGetValues("Request-Id",out var ids); _logger.LogError("Stripe API {Status} at {Path}; requestId={RequestId}; bytes={Bytes}",response.StatusCode,path.Split('?')[0],ids?.FirstOrDefault()??"unknown",Encoding.UTF8.GetByteCount(body)); throw new PaymentGatewayException($"Stripe API error {(int)response.StatusCode}");} try{using var doc=JsonDocument.Parse(body,new JsonDocumentOptions{MaxDepth=32}); return doc.RootElement.Clone();}catch(JsonException ex){throw new PaymentGatewayException("Stripe returned invalid JSON.",ex);}}
    }
    private static async Task<string> ReadLimitedAsync(HttpContent content,CancellationToken ct){if(content.Headers.ContentLength>MaxResponseBytes) throw new PaymentGatewayException("Stripe response too large."); await using var s=await content.ReadAsStreamAsync(ct); using var ms=new MemoryStream(); var b=new byte[16384]; while(true){var n=await s.ReadAsync(b.AsMemory(),ct); if(n==0)break; if(ms.Length+n>MaxResponseBytes) throw new PaymentGatewayException("Stripe response too large."); await ms.WriteAsync(b.AsMemory(0,n),ct);} return Encoding.UTF8.GetString(ms.ToArray());}
    private static string RequiredString(JsonElement e,string p)=>e.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(v.GetString())?v.GetString()!:throw new JsonException($"Missing {p}");
    private static DateTimeOffset RequiredUnix(JsonElement e,string p)=>e.TryGetProperty(p,out var v)&&v.TryGetInt64(out var x)?DateTimeOffset.FromUnixTimeSeconds(x):throw new JsonException($"Missing {p}");
    private static Guid? ReadOrganizationId(JsonElement e){if(!e.TryGetProperty("metadata",out var m)||!m.TryGetProperty("organizationId",out var o)||string.IsNullOrWhiteSpace(o.GetString()))return null; if(Guid.TryParse(o.GetString(),out var id))return id; throw new PaymentWebhookValidationException("Invalid organization metadata.");}
}
