using Microsoft.Extensions.Caching.Memory;

namespace Tenebit.Api.Auth.OAuth;

// CorrelationHash and Nonce bind this transaction to the browser that started it and to the
// specific id_token that must come back — the state alone only proves single-use, not same-browser
// (audyt AUD3-005: OAuth state nie jest związany z przeglądarką).
public sealed record OAuthStateEntry(string Provider, string CodeVerifier, string ReturnPath, string CorrelationHash, string Nonce);

public sealed class OAuthStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _cache;

    public OAuthStateStore(IMemoryCache cache) => _cache = cache;

    public string Create(string provider, string codeVerifier, string returnPath, string correlationHash, string nonce)
    {
        var state = PkceHelper.NewState();
        _cache.Set(CacheKey(state), new OAuthStateEntry(provider, codeVerifier, returnPath, correlationHash, nonce), Ttl);
        return state;
    }

    public OAuthStateEntry? Consume(string state)
    {
        var key = CacheKey(state);
        if (!_cache.TryGetValue(key, out OAuthStateEntry? entry))
        {
            return null;
        }

        _cache.Remove(key);
        return entry;
    }

    private static string CacheKey(string state) => $"oauth-state:{state}";
}
