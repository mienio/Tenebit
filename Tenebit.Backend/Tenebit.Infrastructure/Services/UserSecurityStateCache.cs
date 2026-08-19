using System.Collections.Concurrent;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class UserSecurityStateCache : IUserSecurityStateCache
{
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public bool TryGet(Guid userId, out UserSecurityState state)
    {
        if (_entries.TryGetValue(userId, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            state = entry.State;
            return true;
        }

        _entries.TryRemove(userId, out _);
        state = default!;
        return false;
    }

    public void Set(Guid userId, UserSecurityState state, TimeSpan ttl) =>
        _entries[userId] = new Entry(state, DateTimeOffset.UtcNow.Add(ttl));

    public void Remove(Guid userId) => _entries.TryRemove(userId, out _);

    private sealed record Entry(UserSecurityState State, DateTimeOffset ExpiresAt);
}
