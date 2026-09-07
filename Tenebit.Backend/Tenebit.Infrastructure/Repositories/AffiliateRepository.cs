using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AffiliateRepository : IAffiliateRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateRepository(TenebitDbContext db) => _db = db;

    public Task<Affiliate?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Affiliates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Affiliate?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Affiliates.FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Affiliates.AnyAsync(x => x.Email == normalized, cancellationToken);
    }

    public async Task<AffiliateSecurityState?> GetSecurityStateAsync(Guid id, CancellationToken cancellationToken)
    {
        // "IsActive" here means "allowed to hold a session at all", not "fully approved" - a
        // PendingApproval affiliate can still log in and see their own status in the panel; only
        // Blocked (which also rotates SecurityStamp - see Affiliate.Block) revokes the session.
        return await _db.Affiliates.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AffiliateSecurityState(x.Status != AffiliateStatus.Blocked, x.SecurityStamp, x.IsEmailVerified))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Affiliate>> ListAsync(AffiliateStatus? status, CancellationToken cancellationToken)
    {
        var query = _db.Affiliates.AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public void Add(Affiliate affiliate) => _db.Affiliates.Add(affiliate);
}
