using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Offboarding;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class OffboardingItemRepository : IOffboardingItemRepository
{
    private readonly TenebitDbContext _db;

    public OffboardingItemRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<OffboardingItem>> ListByCaseAsync(Guid organizationId, Guid offboardingCaseId, CancellationToken cancellationToken) =>
        await _db.OffboardingItems
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.OffboardingCaseId == offboardingCaseId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

    public void Add(OffboardingItem item) => _db.OffboardingItems.Add(item);
}
