using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

public interface IAffiliateClickRepository
{
    /// <summary>Used to dedupe/rate-limit bot traffic (spec §6.4/§12.8) - counts recent clicks from the
    /// same hashed IP against the same code, not against the account or a real identity.</summary>
    Task<int> CountRecentAsync(Guid affiliateCodeId, string ipHash, DateTimeOffset since, CancellationToken cancellationToken);
    void Add(AffiliateClick click);
}
