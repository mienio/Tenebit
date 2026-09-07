using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AffiliatePayoutPeriodRepository : IAffiliatePayoutPeriodRepository
{
    private readonly TenebitDbContext _db;
    public AffiliatePayoutPeriodRepository(TenebitDbContext db) => _db = db;

    public Task<AffiliatePayoutPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.AffiliatePayoutPeriods.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<AffiliatePayoutPeriod?> GetOpenForMonthAsync(Guid affiliateId, DateTimeOffset periodStart, CancellationToken cancellationToken) =>
        _db.AffiliatePayoutPeriods.FirstOrDefaultAsync(
            x => x.AffiliateId == affiliateId && x.PeriodStart == periodStart && x.Status == PayoutPeriodStatus.Open,
            cancellationToken);

    public async Task<IReadOnlyList<AffiliatePayoutPeriod>> ListOpenEndingBeforeAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await _db.AffiliatePayoutPeriods
            .Where(x => x.Status == PayoutPeriodStatus.Open && x.PeriodEnd <= now)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AffiliatePayoutPeriod>> ListAwaitingPayoutAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        await _db.AffiliatePayoutPeriods
            .Where(x => x.AffiliateId == affiliateId && x.Status == PayoutPeriodStatus.AwaitingPayout)
            .OrderBy(x => x.PeriodStart)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AffiliatePayoutPeriod>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        await _db.AffiliatePayoutPeriods
            .Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.PeriodStart)
            .ToListAsync(cancellationToken);

    public void Add(AffiliatePayoutPeriod period) => _db.AffiliatePayoutPeriods.Add(period);
}

public sealed class AffiliatePayoutRepository : IAffiliatePayoutRepository
{
    private readonly TenebitDbContext _db;
    public AffiliatePayoutRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AffiliatePayout>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        await _db.AffiliatePayouts
            .Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.MarkedPaidAt)
            .ToListAsync(cancellationToken);

    public void Add(AffiliatePayout payout) => _db.AffiliatePayouts.Add(payout);
}
