using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class ServiceTicketRepository : IServiceTicketRepository
{
    private readonly TenebitDbContext _db;

    public ServiceTicketRepository(TenebitDbContext db) => _db = db;

    public Task<ServiceTicket?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.ServiceTickets.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ServiceTicket>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        await _db.ServiceTickets
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AssetId == assetId)
            .OrderByDescending(x => x.OpenedAt)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<ServiceTicket> Items, int Total)> ListPagedAsync(Guid organizationId, ServiceTicketStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.ServiceTickets.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OpenedAt)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<(IReadOnlyList<ServiceTicket> Items, int Total)> ListPagedScopedAsync(
        Guid organizationId,
        ServiceTicketStatus? status,
        int page,
        int pageSize,
        IReadOnlyCollection<Guid> personIds,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken)
    {
        var allowedAssets = _db.Assets.AsNoTracking()
            .Where(asset => asset.OrganizationId == organizationId
                && ((asset.AssignedPersonId.HasValue && personIds.Contains(asset.AssignedPersonId.Value))
                    || (asset.TeamId.HasValue && teamIds.Contains(asset.TeamId.Value))))
            .Select(asset => asset.Id);
        var query = _db.ServiceTickets.AsNoTracking()
            .Where(ticket => ticket.OrganizationId == organizationId && allowedAssets.Contains(ticket.AssetId));
        return ListPagedCoreAsync(query, status, page, pageSize, cancellationToken);
    }

    private static async Task<(IReadOnlyList<ServiceTicket> Items, int Total)> ListPagedCoreAsync(
        IQueryable<ServiceTicket> query,
        ServiceTicketStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.OpenedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public void Add(ServiceTicket ticket) => _db.ServiceTickets.Add(ticket);
}
