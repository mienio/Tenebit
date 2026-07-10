using Microsoft.Extensions.Caching.Memory;

namespace Tenebit.Api.Auth.OAuth;

public sealed record OAuthStateEntry(string Provider, string CodeVerifier, string ReturnPath);

public sealed class OAuthStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _cache;

    public OAuthStateStore(IMemoryCache cache) => _cache = cache;

    public string Create(string provider, string codeVerifier, string returnPath)
    {
        var state = PkceHelper.NewState();
        _cache.Set(CacheKey(state), new OAuthStateEntry(provider, codeVerifier, returnPath), Ttl);
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
