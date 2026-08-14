using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Reservations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class EquipmentKitDefinitionRepository : IEquipmentKitDefinitionRepository
{
    private readonly TenebitDbContext _db;

    public EquipmentKitDefinitionRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<EquipmentKitDefinition>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.EquipmentKitDefinitions
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<EquipmentKitDefinition?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.EquipmentKitDefinitions
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public void Add(EquipmentKitDefinition kitDefinition) => _db.EquipmentKitDefinitions.Add(kitDefinition);
}
