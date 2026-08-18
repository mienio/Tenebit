using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly TenebitDbContext _db;
    public RefreshTokenRepository(TenebitDbContext db) => _db = db;

    public Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
        _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task<RefreshToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > now, cancellationToken);

    public async Task<bool> TryMarkRotatedAsync(Guid tokenId, Guid replacementTokenId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var affected = await _db.RefreshTokens
            .Where(x => x.Id == tokenId && x.RevokedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.ReplacedByTokenId, replacementTokenId)
                .SetProperty(x => x.RevocationReason, "rotated"), cancellationToken);
        return affected == 1;
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        await _db.RefreshTokens
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.RevocationReason, reason), cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid organizationUserId, CancellationToken cancellationToken)
    {
        var tokens = await _db.RefreshTokens
            .Where(x => x.OrganizationUserId == organizationUserId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens) token.Revoke(reason: "security_state_changed");
    }

    public void Add(RefreshToken token) => _db.RefreshTokens.Add(token);
}
