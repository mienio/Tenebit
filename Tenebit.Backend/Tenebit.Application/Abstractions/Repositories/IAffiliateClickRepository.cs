using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

public interface IAffiliateClickRepository
{
    /// <summary>Used to dedupe/rate-limit bot traffic (spec §6.4/§12.8) - counts recent clicks from the
    /// same hashed IP against the same code, not against the account or a real identity.</summary>
    Task<int> CountRecentAsync(Guid affiliateCodeId, string ipHash, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Resolves the <c>tnb_aff</c> attribution cookie back to the code it was issued for, at
    /// checkout time (spec §13.1) - the click's own <see cref="AffiliateClick.AffiliateCodeId"/>, never
    /// trusting anything the frontend itself might claim about which code applies.</summary>
    Task<Guid?> FindAffiliateCodeIdByAttributionTokenAsync(Guid attributionToken, CancellationToken cancellationToken);
    void Add(AffiliateClick click);
}
