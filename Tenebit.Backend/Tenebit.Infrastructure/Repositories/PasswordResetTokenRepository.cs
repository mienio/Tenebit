using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly TenebitDbContext _db;
    public PasswordResetTokenRepository(TenebitDbContext db) => _db = db;

    public Task<PasswordResetToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.PasswordResetTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now, cancellationToken);

    public async Task<Guid?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Read only the immutable owner id, then atomically compare-and-set UsedAt. Two concurrent
        // consumers may both see the candidate, but exactly one UPDATE can affect a row.
        var candidate = await _db.PasswordResetTokens.AsNoTracking()
            .Where(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now)
            .Select(x => new { x.Id, x.OrganizationUserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return null;

        var affected = await _db.PasswordResetTokens
            .Where(x => x.Id == candidate.Id && x.UsedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);
        return affected == 1 ? candidate.OrganizationUserId : null;
    }

    public Task RevokeUnusedForUserAsync(Guid organizationUserId, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.PasswordResetTokens
            .Where(x => x.OrganizationUserId == organizationUserId && x.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);

    public void Add(PasswordResetToken token) => _db.PasswordResetTokens.Add(token);
}
