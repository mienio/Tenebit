using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;

namespace Tenebit.Tests.Fakes;

public sealed class InMemoryOAuthTransactionRepository : IOAuthTransactionRepository
{
    private readonly List<OAuthTransaction> _items = [];
    private readonly HashSet<Guid> _consumed = [];
    private readonly object _gate = new();
    public void Add(OAuthTransaction transaction) { lock (_gate) _items.Add(transaction); }
    public Task<OAuthTransaction?> TryConsumeAsync(string stateHash, string provider, string correlationHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(x => x.StateHash == stateHash && x.Provider == provider && x.CorrelationHash == correlationHash && x.ExpiresAt > now && !_consumed.Contains(x.Id));
            if (item is not null) _consumed.Add(item.Id);
            return Task.FromResult(item);
        }
    }
}

public sealed class InMemoryTwoFactorChallengeRepository : ITwoFactorChallengeRepository
{
    private readonly List<TwoFactorChallenge> _items = [];
    private readonly HashSet<Guid> _consumed = [];
    private readonly object _gate = new();
    public void Add(TwoFactorChallenge challenge) { lock (_gate) _items.Add(challenge); }
    public Task<TwoFactorChallenge?> TryConsumeAsync(string ticketHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(x => x.TicketHash == ticketHash && x.ExpiresAt > now && !_consumed.Contains(x.Id));
            if (item is not null) _consumed.Add(item.Id);
            return Task.FromResult(item);
        }
    }
}
