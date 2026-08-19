using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly TenebitDbContext _db;
    public EmailVerificationTokenRepository(TenebitDbContext db) => _db = db;

    public Task<EmailVerificationToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.EmailVerificationTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now, cancellationToken);

    public async Task<Guid?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidate = await _db.EmailVerificationTokens.AsNoTracking()
            .Where(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now)
            .Select(x => new { x.Id, x.OrganizationUserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return null;
        var affected = await _db.EmailVerificationTokens
            .Where(x => x.Id == candidate.Id && x.UsedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);
        return affected == 1 ? candidate.OrganizationUserId : null;
    }

    public Task RevokeUnusedForUserAsync(Guid organizationUserId, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.EmailVerificationTokens
            .Where(x => x.OrganizationUserId == organizationUserId && x.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);

    public void Add(EmailVerificationToken token) => _db.EmailVerificationTokens.Add(token);
}
