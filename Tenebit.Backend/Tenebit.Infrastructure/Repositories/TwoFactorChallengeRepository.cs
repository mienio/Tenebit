using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class TwoFactorChallengeRepository : ITwoFactorChallengeRepository
{
    private readonly TenebitDbContext _db;
    public TwoFactorChallengeRepository(TenebitDbContext db) => _db = db;

    public void Add(TwoFactorChallenge challenge) => _db.TwoFactorChallenges.Add(challenge);

    public async Task<TwoFactorChallenge?> TryConsumeAsync(string ticketHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entry = await _db.TwoFactorChallenges.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TicketHash == ticketHash && x.ConsumedAt == null && x.ExpiresAt > now, cancellationToken);
        if (entry is null) return null;

        var claimed = await _db.TwoFactorChallenges
            .Where(x => x.Id == entry.Id && x.ConsumedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.ConsumedAt, now), cancellationToken);

        return claimed == 1 ? entry : null;
    }
}
