using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AffiliateRefreshTokenRepository : IAffiliateRefreshTokenRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateRefreshTokenRepository(TenebitDbContext db) => _db = db;

    public Task<AffiliateRefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
        _db.AffiliateRefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task<AffiliateRefreshToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.AffiliateRefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > now, cancellationToken);

    public async Task<bool> TryMarkRotatedAsync(Guid tokenId, Guid replacementTokenId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var affected = await _db.AffiliateRefreshTokens
            .Where(x => x.Id == tokenId && x.RevokedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.ReplacedByTokenId, replacementTokenId)
                .SetProperty(x => x.RevocationReason, "rotated"), cancellationToken);
        return affected == 1;
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        await _db.AffiliateRefreshTokens
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.RevocationReason, reason), cancellationToken);
    }

    public async Task RevokeAllForAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken)
    {
        var tokens = await _db.AffiliateRefreshTokens
            .Where(x => x.AffiliateId == affiliateId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens) token.Revoke(reason: "security_state_changed");
    }

    public void Add(AffiliateRefreshToken token) => _db.AffiliateRefreshTokens.Add(token);
}

public sealed class AffiliatePasswordResetTokenRepository : IAffiliatePasswordResetTokenRepository
{
    private readonly TenebitDbContext _db;
    public AffiliatePasswordResetTokenRepository(TenebitDbContext db) => _db = db;

    public Task<AffiliatePasswordResetToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.AffiliatePasswordResetTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now, cancellationToken);

    public async Task<Guid?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidate = await _db.AffiliatePasswordResetTokens.AsNoTracking()
            .Where(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now)
            .Select(x => new { x.Id, x.AffiliateId })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return null;

        var affected = await _db.AffiliatePasswordResetTokens
            .Where(x => x.Id == candidate.Id && x.UsedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);
        return affected == 1 ? candidate.AffiliateId : null;
    }

    public Task RevokeUnusedForAffiliateAsync(Guid affiliateId, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.AffiliatePasswordResetTokens
            .Where(x => x.AffiliateId == affiliateId && x.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);

    public void Add(AffiliatePasswordResetToken token) => _db.AffiliatePasswordResetTokens.Add(token);
}

public sealed class AffiliateEmailVerificationTokenRepository : IAffiliateEmailVerificationTokenRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateEmailVerificationTokenRepository(TenebitDbContext db) => _db = db;

    public Task<AffiliateEmailVerificationToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.AffiliateEmailVerificationTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now, cancellationToken);

    public async Task<Guid?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidate = await _db.AffiliateEmailVerificationTokens.AsNoTracking()
            .Where(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now)
            .Select(x => new { x.Id, x.AffiliateId })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return null;

        var affected = await _db.AffiliateEmailVerificationTokens
            .Where(x => x.Id == candidate.Id && x.UsedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);
        return affected == 1 ? candidate.AffiliateId : null;
    }

    public Task RevokeUnusedForAffiliateAsync(Guid affiliateId, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.AffiliateEmailVerificationTokens
            .Where(x => x.AffiliateId == affiliateId && x.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);

    public void Add(AffiliateEmailVerificationToken token) => _db.AffiliateEmailVerificationTokens.Add(token);
}
