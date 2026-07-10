using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.People;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class TeamRepository : ITeamRepository
{
    private readonly TenebitDbContext _db;
    public TeamRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<Team>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.Teams.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<Team?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Teams.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingTeamId, CancellationToken cancellationToken) =>
        _db.Teams.AnyAsync(x => x.OrganizationId == organizationId && x.Name == name.Trim() && (!excludingTeamId.HasValue || x.Id != excludingTeamId.Value), cancellationToken);

    public void Add(Team team) => _db.Teams.Add(team);
}
