using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

public interface IAffiliateConversionRepository
{
    Task<bool> ExistsByPaddleTransactionAsync(string paddleTransactionId, CancellationToken cancellationToken);
    Task<AffiliateConversion?> GetByPaddleTransactionAsync(string paddleTransactionId, CancellationToken cancellationToken);

    /// <summary>Earliest InitialSale conversion for this affiliate code + organization pair - the
    /// anchor date <see cref="AffiliateConversion.IsWithinCommissionWindow"/> is computed from for
    /// every later renewal (spec §6.2 pkt 4).</summary>
    Task<DateTimeOffset?> GetFirstSaleDateAsync(Guid affiliateCodeId, Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AffiliateConversion>> ListByAffiliateAsync(Guid affiliateId, int page, int pageSize, CancellationToken cancellationToken);
    Task<int> CountByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliateConversion>> ListRequiringReviewAsync(CancellationToken cancellationToken);

    /// <summary>Every cleared (non-review) conversion for the affiliate that occurred inside
    /// [periodStart, periodEnd) and has not yet been assigned to a payout period - what the period
    /// close job aggregates.</summary>
    Task<IReadOnlyList<AffiliateConversion>> ListUnassignedInRangeAsync(Guid affiliateId, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken);

    void Add(AffiliateConversion conversion);
}
