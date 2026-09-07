using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AffiliateConversionRepository : IAffiliateConversionRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateConversionRepository(TenebitDbContext db) => _db = db;

    public Task<bool> ExistsByPaddleTransactionAsync(string paddleTransactionId, CancellationToken cancellationToken) =>
        _db.AffiliateConversions.AnyAsync(x => x.PaddleTransactionId == paddleTransactionId, cancellationToken);

    public Task<AffiliateConversion?> GetByPaddleTransactionAsync(string paddleTransactionId, CancellationToken cancellationToken) =>
        _db.AffiliateConversions.FirstOrDefaultAsync(x => x.PaddleTransactionId == paddleTransactionId, cancellationToken);

    public Task<DateTimeOffset?> GetFirstSaleDateAsync(Guid affiliateCodeId, Guid organizationId, CancellationToken cancellationToken) =>
        _db.AffiliateConversions
            .Where(x => x.AffiliateCodeId == affiliateCodeId && x.OrganizationId == organizationId && x.EventType == AffiliateConversionEventType.InitialSale)
            .OrderBy(x => x.OccurredAt)
            .Select(x => (DateTimeOffset?)x.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AffiliateConversion>> ListByAffiliateAsync(Guid affiliateId, int page, int pageSize, CancellationToken cancellationToken) =>
        await _db.AffiliateConversions.Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public Task<int> CountByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        _db.AffiliateConversions.CountAsync(x => x.AffiliateId == affiliateId, cancellationToken);

    public async Task<IReadOnlyList<AffiliateConversion>> ListRequiringReviewAsync(CancellationToken cancellationToken) =>
        await _db.AffiliateConversions.Where(x => x.RequiresReview)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AffiliateConversion>> ListUnassignedInRangeAsync(Guid affiliateId, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken) =>
        await _db.AffiliateConversions
            .Where(x => x.AffiliateId == affiliateId
                && !x.RequiresReview
                && x.AffiliatePayoutPeriodId == null
                && x.OccurredAt >= periodStart && x.OccurredAt < periodEnd)
            .ToListAsync(cancellationToken);

    public void Add(AffiliateConversion conversion) => _db.AffiliateConversions.Add(conversion);
}
