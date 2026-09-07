using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

/// <summary>Mirrors <see cref="IRefreshTokenRepository"/> 1:1 against a fully separate table (see
/// <see cref="AffiliateRefreshToken"/>).</summary>
public interface IAffiliateRefreshTokenRepository
{
    Task<AffiliateRefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken);
    Task<AffiliateRefreshToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> TryMarkRotatedAsync(Guid tokenId, Guid replacementTokenId, DateTimeOffset now, CancellationToken cancellationToken);
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken);
    Task RevokeAllForAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken);
    void Add(AffiliateRefreshToken token);
}

/// <summary>Mirrors <see cref="IPasswordResetTokenRepository"/> 1:1 against
/// <see cref="AffiliatePasswordResetToken"/>.</summary>
public interface IAffiliatePasswordResetTokenRepository
{
    Task<AffiliatePasswordResetToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    Task<Guid?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    Task RevokeUnusedForAffiliateAsync(Guid affiliateId, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(AffiliatePasswordResetToken token);
}

/// <summary>Mirrors <see cref="IEmailVerificationTokenRepository"/> 1:1 against
/// <see cref="AffiliateEmailVerificationToken"/>.</summary>
public interface IAffiliateEmailVerificationTokenRepository
{
    Task<AffiliateEmailVerificationToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    Task<Guid?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    Task RevokeUnusedForAffiliateAsync(Guid affiliateId, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(AffiliateEmailVerificationToken token);
}
