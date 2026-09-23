using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Keeps an in-memory snapshot of the addresses Paddle delivers webhooks from, refreshed from Paddle's own
/// <c>/ips</c> endpoint. The list is never hard-coded: Paddle documents that endpoint as the source of
/// truth precisely because the addresses change, and a copy pasted into config would turn a routine change
/// on their side into every webhook being rejected here.
///
/// Sandbox and production publish different sets, so the host follows Paddle:Environment the same way
/// <see cref="PaddlePaymentGateway"/> picks its API base.
/// </summary>
public sealed class PaddleIpAllowlist : IPaddleIpAllowlist
{
    // internal, not private: ParseResponse/Replace expose it across the assembly for the refresher below.
    internal readonly record struct Cidr(uint Network, uint Mask)
    {
        public bool Contains(uint address) => (address & Mask) == Network;
    }

    // Replaced wholesale on every refresh rather than mutated, so a webhook arriving mid-refresh reads
    // either the old list or the new one and never a half-built one.
    private volatile IReadOnlyList<Cidr>? _cidrs;

    public bool IsAllowed(IPAddress? remoteIp)
    {
        var cidrs = _cidrs;
        if (cidrs is null) return true; // never fetched - see IPaddleIpAllowlist for why this is not a deny
        if (remoteIp is null) return false;

        // Kestrel hands back ::ffff:a.b.c.d for an IPv4 peer on a dual-stack socket. Paddle publishes
        // ipv4_cidrs only, so the mapped form has to be folded back or every real delivery looks foreign.
        var ipv4 = remoteIp.AddressFamily == AddressFamily.InterNetworkV6 && remoteIp.IsIPv4MappedToIPv6
            ? remoteIp.MapToIPv4()
            : remoteIp;

        // A genuine IPv6 peer is unjudgeable, not suspicious: Paddle's /ips publishes ipv4_cidrs and
        // nothing else, so there is no list to check such an address against. Denying it would mean that
        // the day Paddle adds IPv6 egress, every webhook starts failing and paying customers stop being
        // provisioned - the same trade-off as the never-fetched case above, and the signature check still
        // stands behind it. Revisit if Paddle ever starts publishing ipv6 ranges.
        if (ipv4.AddressFamily != AddressFamily.InterNetwork) return true;

        var value = ToUInt32(ipv4);
        foreach (var cidr in cidrs)
            if (cidr.Contains(value)) return true;
        return false;
    }

    internal void Replace(IReadOnlyList<Cidr> cidrs) => _cidrs = cidrs;

    internal bool HasList => _cidrs is not null;

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    /// <summary>Parses "34.237.3.244/32". Returns null for anything it cannot read, so one malformed entry
    /// cannot take the whole list down with it.</summary>
    internal static Cidr? ParseCidr(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var slash = value.IndexOf('/');
        var addressPart = slash < 0 ? value : value[..slash];
        if (!IPAddress.TryParse(addressPart.Trim(), out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            return null;

        var prefix = 32;
        if (slash >= 0 && (!int.TryParse(value[(slash + 1)..].Trim(), out prefix) || prefix is < 0 or > 32))
            return null;

        // A /0 mask cannot be produced by shifting a 32-bit value by 32 (undefined in C#, and in practice a
        // no-op that would yield 0xFFFFFFFF), so it is spelled out.
        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        return new Cidr(ToUInt32(address) & mask, mask);
    }

    internal static IReadOnlyList<Cidr> ParseResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("ipv4_cidrs", out var list)
            || list.ValueKind != JsonValueKind.Array)
            return [];

        var parsed = new List<Cidr>();
        foreach (var entry in list.EnumerateArray())
        {
            var cidr = ParseCidr(entry.GetString());
            if (cidr is not null) parsed.Add(cidr.Value);
        }
        return parsed;
    }
}

/// <summary>
/// Refreshes <see cref="PaddleIpAllowlist"/> on a timer. Runs off the request path on purpose: a webhook
/// must never wait on an outbound call to Paddle to decide whether to accept the one it is already holding.
/// </summary>
public sealed class PaddleIpAllowlistRefresher : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

    private readonly PaddleIpAllowlist _allowlist;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaddleIpAllowlistRefresher> _logger;

    public PaddleIpAllowlistRefresher(
        PaddleIpAllowlist allowlist,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PaddleIpAllowlistRefresher> logger)
    {
        _allowlist = allowlist;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private string Endpoint =>
        string.Equals(_configuration["Paddle:Environment"], "production", StringComparison.OrdinalIgnoreCase)
            ? "https://api.paddle.com/ips"
            : "https://sandbox-api.paddle.com/ips";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var refreshed = await TryRefreshAsync(stoppingToken);
            try
            {
                // Back off to a short retry only while we still have nothing: once a list is in hand a
                // failed refresh is harmless, the old one stays valid and the normal cadence is fine.
                await Task.Delay(refreshed || _allowlist.HasList ? RefreshInterval : RetryInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(PaddleIpAllowlist));
            client.Timeout = TimeSpan.FromSeconds(15);
            var json = await client.GetStringAsync(Endpoint, cancellationToken);

            var cidrs = PaddleIpAllowlist.ParseResponse(json);
            if (cidrs.Count == 0)
            {
                // An empty list would deny every delivery. Treat it as a bad response and keep whatever we
                // already had rather than locking the webhook out on Paddle's say-so.
                _logger.LogWarning("Paddle {Endpoint} returned no usable ipv4_cidrs; keeping the previous allowlist.", Endpoint);
                return false;
            }

            _allowlist.Replace(cidrs);
            _logger.LogInformation("Paddle webhook IP allowlist refreshed: {Count} ranges from {Endpoint}.", cidrs.Count, Endpoint);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh the Paddle webhook IP allowlist from {Endpoint}.", Endpoint);
            return false;
        }
    }
}
