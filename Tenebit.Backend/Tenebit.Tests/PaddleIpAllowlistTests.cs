using System.Net;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class PaddleIpAllowlistTests
{
    /// <summary>The exact shape Paddle's GET /ips returns, captured from the live endpoint on 2026-09-23.</summary>
    private const string LiveResponse = """
        {
          "data": {
            "ipv4_cidrs": [
              "34.237.3.244/32",
              "34.195.105.136/32",
              "34.232.58.13/32",
              "35.155.119.135/32",
              "34.212.5.7/32",
              "52.11.166.252/32"
            ]
          },
          "meta": { "request_id": "14640ffc-df73-4dd7-a4e2-e768bc17786c" }
        }
        """;

    private static PaddleIpAllowlist Loaded(string json = LiveResponse)
    {
        var allowlist = new PaddleIpAllowlist();
        allowlist.Replace(PaddleIpAllowlist.ParseResponse(json));
        return allowlist;
    }

    [Fact]
    public void Allows_everything_until_a_list_has_been_fetched()
    {
        // Deliberate: the Paddle-Signature HMAC is the real authentication, and refusing genuine
        // subscription events because Paddle's /ips endpoint was unreachable at startup would cost paying
        // customers their access over a secondary control.
        var allowlist = new PaddleIpAllowlist();

        Assert.False(allowlist.HasList);
        Assert.True(allowlist.IsAllowed(IPAddress.Parse("203.0.113.9")));
        Assert.True(allowlist.IsAllowed(null));
    }

    [Fact]
    public void Allows_every_address_paddle_publishes()
    {
        var allowlist = Loaded();

        foreach (var address in new[] { "34.237.3.244", "34.195.105.136", "34.232.58.13", "35.155.119.135", "34.212.5.7", "52.11.166.252" })
            Assert.True(allowlist.IsAllowed(IPAddress.Parse(address)), address);
    }

    [Fact]
    public void Rejects_an_address_paddle_does_not_publish()
    {
        var allowlist = Loaded();

        Assert.False(allowlist.IsAllowed(IPAddress.Parse("203.0.113.9")));
        // One off either end of a /32 - the mask must not be wider than Paddle says.
        Assert.False(allowlist.IsAllowed(IPAddress.Parse("34.237.3.243")));
        Assert.False(allowlist.IsAllowed(IPAddress.Parse("34.237.3.245")));
    }

    [Fact]
    public void Accepts_a_paddle_address_arriving_as_ipv4_mapped_ipv6()
    {
        // Kestrel reports ::ffff:a.b.c.d for an IPv4 peer on a dual-stack socket. Paddle publishes IPv4
        // only, so without folding this back every real delivery would be rejected.
        var allowlist = Loaded();

        Assert.True(allowlist.IsAllowed(IPAddress.Parse("::ffff:34.237.3.244")));
        Assert.False(allowlist.IsAllowed(IPAddress.Parse("::ffff:203.0.113.9")));
    }

    [Fact]
    public void Rejects_a_real_ipv6_peer_and_an_unknown_one_once_the_list_is_known()
    {
        var allowlist = Loaded();

        Assert.False(allowlist.IsAllowed(IPAddress.Parse("2001:db8::1")));
        Assert.False(allowlist.IsAllowed(null));
    }

    [Fact]
    public void Honours_a_prefix_wider_than_32_if_paddle_ever_publishes_one()
    {
        var allowlist = Loaded("""{"data":{"ipv4_cidrs":["198.51.100.0/24"]}}""");

        Assert.True(allowlist.IsAllowed(IPAddress.Parse("198.51.100.0")));
        Assert.True(allowlist.IsAllowed(IPAddress.Parse("198.51.100.255")));
        Assert.False(allowlist.IsAllowed(IPAddress.Parse("198.51.101.0")));
    }

    [Fact]
    public void Skips_unreadable_entries_instead_of_dropping_the_whole_list()
    {
        var allowlist = Loaded("""{"data":{"ipv4_cidrs":["not-an-ip","34.237.3.244/32","10.0.0.1/99","2001:db8::/32",""]}}""");

        Assert.True(allowlist.IsAllowed(IPAddress.Parse("34.237.3.244")));
        Assert.False(allowlist.IsAllowed(IPAddress.Parse("10.0.0.1")));
    }

    [Fact]
    public void Reads_an_unexpected_payload_as_empty_rather_than_throwing()
    {
        // The refresher treats an empty result as a bad response and keeps the previous list, so this must
        // come back empty rather than blow up the background service.
        Assert.Empty(PaddleIpAllowlist.ParseResponse("""{"data":{}}"""));
        Assert.Empty(PaddleIpAllowlist.ParseResponse("""{"meta":{"request_id":"x"}}"""));
        Assert.Empty(PaddleIpAllowlist.ParseResponse("""{"data":{"ipv4_cidrs":"34.237.3.244/32"}}"""));
    }
}
