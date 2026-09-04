using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IPromoCodeRepository
{
    Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<PromoCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PromoCode>> ListAsync(CancellationToken cancellationToken);
    void Add(PromoCode promoCode);
    void Remove(PromoCode promoCode);
}
