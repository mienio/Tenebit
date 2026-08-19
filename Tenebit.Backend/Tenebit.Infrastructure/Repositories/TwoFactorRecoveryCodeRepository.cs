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

    public async Task<bool> TryConsumeAsync(Guid organizationUserId, string codeHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var affected = await _db.TwoFactorRecoveryCodes
            .Where(x => x.OrganizationUserId == organizationUserId && x.CodeHash == codeHash && x.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), cancellationToken);
        return affected == 1;
    }

    public void AddRange(IEnumerable<TwoFactorRecoveryCode> codes) => _db.TwoFactorRecoveryCodes.AddRange(codes);
    public void RemoveAll(IEnumerable<TwoFactorRecoveryCode> codes) => _db.TwoFactorRecoveryCodes.RemoveRange(codes);
}
