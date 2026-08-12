using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Dashboards;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

internal sealed class DashboardLayoutRepository : IDashboardLayoutRepository
{
    private readonly TenebitDbContext _context;

    public DashboardLayoutRepository(TenebitDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardLayout?> GetAsync(Guid organizationUserId, CancellationToken cancellationToken)
    {
        return await _context.DashboardLayouts
            .Where(x => x.OrganizationUserId == organizationUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(DashboardLayout layout)
    {
        _context.DashboardLayouts.Add(layout);
    }
}
