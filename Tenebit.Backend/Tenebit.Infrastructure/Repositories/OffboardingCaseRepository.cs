using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Offboarding;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class OffboardingCaseRepository : IOffboardingCaseRepository
{
    private static readonly OffboardingCaseStatus[] ClosedStatuses = [OffboardingCaseStatus.Completed, OffboardingCaseStatus.Cancelled];

    private readonly TenebitDbContext _db;

    public OffboardingCaseRepository(TenebitDbContext db) => _db = db;

    public async Task<(IReadOnlyList<OffboardingCase> Items, int Total)> ListPagedAsync(Guid organizationId, OffboardingCaseStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.OffboardingCases
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId);

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<OffboardingCase>> ListOpenAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.OffboardingCases
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && (x.Status == OffboardingCaseStatus.Active || x.Status == OffboardingCaseStatus.WaitingForReturn))
            .ToListAsync(cancellationToken);

    public Task<OffboardingCase?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.OffboardingCases.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<OffboardingCase?> FindOpenByPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        _db.OffboardingCases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.PersonId == personId && !ClosedStatuses.Contains(x.Status), cancellationToken);

    public Task<OffboardingCase?> FindByPublicTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        _db.OffboardingCases.FirstOrDefaultAsync(x => x.PublicTokenHash == tokenHash, cancellationToken);

    public void Add(OffboardingCase offboardingCase) => _db.OffboardingCases.Add(offboardingCase);
}
