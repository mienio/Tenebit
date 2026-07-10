using Microsoft.Extensions.Caching.Memory;
using Tenebit.Api.Auth.OAuth;

namespace Tenebit.Api.Auth;

public sealed class TwoFactorChallengeStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private readonly IMemoryCache _cache;

    public TwoFactorChallengeStore(IMemoryCache cache) => _cache = cache;

    public string Create(Guid userId)
    {
        var ticket = PkceHelper.NewState();
        _cache.Set(CacheKey(ticket), userId, Ttl);
        return ticket;
    }

    public Guid? Consume(string ticket)
    {
        var key = CacheKey(ticket);
        if (!_cache.TryGetValue(key, out Guid userId))
        {
            return null;
        }

        _cache.Remove(key);
        return userId;
    }

    private static string CacheKey(string ticket) => $"2fa-challenge:{ticket}";
}
