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
        _db.DeviceTrustTokens.FirstOrDefaultAsync(x => x.OrganizationUserId == organizationUserId && x.TokenHash == tokenHash && x.ExpiresAt > now, cancellationToken);

    public void Add(DeviceTrustToken token) => _db.DeviceTrustTokens.Add(token);
}
