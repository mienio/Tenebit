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

    public Task RevokeAllForUserAsync(Guid organizationUserId, CancellationToken cancellationToken) =>
        _db.RefreshTokens
            .Where(x => x.OrganizationUserId == organizationUserId && x.RevokedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);

    public void Add(RefreshToken token) => _db.RefreshTokens.Add(token);
}
