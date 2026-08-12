using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class TwoFactorRecoveryCodeRepository : ITwoFactorRecoveryCodeRepository
{
    private readonly TenebitDbContext _db;
    public TwoFactorRecoveryCodeRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<TwoFactorRecoveryCode>> ListAsync(Guid organizationUserId, CancellationToken cancellationToken) =>
        await _db.TwoFactorRecoveryCodes.Where(x => x.OrganizationUserId == organizationUserId).ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<TwoFactorRecoveryCode> codes) => _db.TwoFactorRecoveryCodes.AddRange(codes);
    public void RemoveAll(IEnumerable<TwoFactorRecoveryCode> codes) => _db.TwoFactorRecoveryCodes.RemoveRange(codes);
}
