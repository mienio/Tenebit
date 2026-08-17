using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly TenebitDbContext _db;
    public RefreshTokenRepository(TenebitDbContext db) => _db = db;

    public Task<RefreshToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > now, cancellationToken);

    public async Task<RefreshToken?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var token = await _db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (token is null || !token.IsValid(now))
        {
            return null;
        }

        // The WHERE RevokedAt == null is the actual race gate — only one concurrent caller's UPDATE
        // affects a row; a loser sees affected == 0 and must treat the token as already consumed.
        var claimed = await _db.RefreshTokens
            .Where(x => x.Id == token.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, now), cancellationToken);

        return claimed == 1 ? token : null;
    }

    // Change-tracked (not ExecuteUpdateAsync) so this participates in the caller's single
    // SaveChangesAsync transaction — e.g. atomic with a password reset or role change in the same
    // unit of work, instead of committing as its own separate statement (audyt AUD3-012).
    public async Task RevokeAllForUserAsync(Guid organizationUserId, CancellationToken cancellationToken)
    {
        var tokens = await _db.RefreshTokens
            .Where(x => x.OrganizationUserId == organizationUserId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke();
        }
    }

    public void Add(RefreshToken token) => _db.RefreshTokens.Add(token);
}
