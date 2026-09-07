using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

public interface IAffiliateCodeRepository
{
    Task<AffiliateCode?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<AffiliateCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliateCode>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken);
    Task<int> CountActiveByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken);
    void Add(AffiliateCode code);
}
