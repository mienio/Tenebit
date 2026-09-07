using System.Collections.Concurrent;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class AffiliateSecurityStateCache : IAffiliateSecurityStateCache
{
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public bool TryGet(Guid affiliateId, out AffiliateSecurityState state)
    {
        if (_entries.TryGetValue(affiliateId, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            state = entry.State;
            return true;
        }

        _entries.TryRemove(affiliateId, out _);
        state = default!;
        return false;
    }

    public void Set(Guid affiliateId, AffiliateSecurityState state, TimeSpan ttl) =>
        _entries[affiliateId] = new Entry(state, DateTimeOffset.UtcNow.Add(ttl));

    public void Remove(Guid affiliateId) => _entries.TryRemove(affiliateId, out _);

    private sealed record Entry(AffiliateSecurityState State, DateTimeOffset ExpiresAt);
}
