using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

public sealed record AffiliateSecurityState(bool IsActive, Guid SecurityStamp, bool IsEmailVerified);

public interface IAffiliateRepository
{
    Task<Affiliate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Affiliate?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<AffiliateSecurityState?> GetSecurityStateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Affiliate>> ListAsync(AffiliateStatus? status, CancellationToken cancellationToken);
    void Add(Affiliate affiliate);
}
