using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

public interface IAffiliatePayoutPeriodRepository
{
    Task<AffiliatePayoutPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AffiliatePayoutPeriod?> GetOpenForMonthAsync(Guid affiliateId, DateTimeOffset periodStart, CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliatePayoutPeriod>> ListOpenEndingBeforeAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliatePayoutPeriod>> ListAwaitingPayoutAsync(Guid affiliateId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliatePayoutPeriod>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken);
    void Add(AffiliatePayoutPeriod period);
}

public interface IAffiliatePayoutRepository
{
    Task<IReadOnlyList<AffiliatePayout>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken);
    void Add(AffiliatePayout payout);
}
