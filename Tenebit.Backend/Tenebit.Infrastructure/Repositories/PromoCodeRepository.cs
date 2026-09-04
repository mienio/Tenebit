using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Subscriptions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

internal sealed class PromoCodeRepository : IPromoCodeRepository
{
    private readonly TenebitDbContext _context;

    public PromoCodeRepository(TenebitDbContext context)
    {
        _context = context;
    }

    public async Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.PromoCodes
            .Where(x => x.Code == normalized)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PromoCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.PromoCodes
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PromoCode>> ListAsync(CancellationToken cancellationToken)
    {
        return await _context.PromoCodes
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(PromoCode promoCode) => _context.PromoCodes.Add(promoCode);

    public void Remove(PromoCode promoCode) => _context.PromoCodes.Remove(promoCode);
}
