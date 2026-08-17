using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class DeviceTrustTokenRepository : IDeviceTrustTokenRepository
{
    private readonly TenebitDbContext _db;
    public DeviceTrustTokenRepository(TenebitDbContext db) => _db = db;

    public Task<DeviceTrustToken?> FindValidAsync(Guid organizationUserId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.DeviceTrustTokens.FirstOrDefaultAsync(x => x.OrganizationUserId == organizationUserId && x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > now, cancellationToken);

    // Change-tracked (not ExecuteUpdateAsync) so this participates in the caller's single
    // SaveChangesAsync transaction instead of committing as its own separate statement (audyt AUD3-012).
    public async Task RevokeAllForUserAsync(Guid organizationUserId, CancellationToken cancellationToken)
    {
        var tokens = await _db.DeviceTrustTokens
            .Where(x => x.OrganizationUserId == organizationUserId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke();
        }
    }

    public void Add(DeviceTrustToken token) => _db.DeviceTrustTokens.Add(token);
}
