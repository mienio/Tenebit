using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class OAuthTransactionRepository : IOAuthTransactionRepository
{
    private readonly TenebitDbContext _db;
    public OAuthTransactionRepository(TenebitDbContext db) => _db = db;

    public void Add(OAuthTransaction transaction) => _db.OAuthTransactions.Add(transaction);

    public async Task<OAuthTransaction?> TryConsumeAsync(string stateHash, string provider, string correlationHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entry = await _db.OAuthTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StateHash == stateHash && x.Provider == provider && x.CorrelationHash == correlationHash && x.ConsumedAt == null && x.ExpiresAt > now, cancellationToken);
        if (entry is null) return null;

        var claimed = await _db.OAuthTransactions
            .Where(x => x.Id == entry.Id && x.ConsumedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.ConsumedAt, now), cancellationToken);

        return claimed == 1 ? entry : null;
    }
}
