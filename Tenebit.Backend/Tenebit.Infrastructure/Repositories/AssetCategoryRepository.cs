using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetCategoryRepository : IAssetCategoryRepository
{
    private readonly TenebitDbContext _db;
    public AssetCategoryRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetCategory>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.AssetCategories.Include(x => x.FieldDefinitions).Where(x => x.OrganizationId == organizationId).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<AssetCategory?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.AssetCategories.Include(x => x.FieldDefinitions).FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingCategoryId, CancellationToken cancellationToken) =>
        _db.AssetCategories.AnyAsync(x => x.OrganizationId == organizationId && x.Name == name.Trim() && (!excludingCategoryId.HasValue || x.Id != excludingCategoryId.Value), cancellationToken);

    public Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Assets.AnyAsync(x => x.OrganizationId == organizationId && x.CategoryId == id, cancellationToken);

    public void Add(AssetCategory category) => _db.AssetCategories.Add(category);
    public void Remove(AssetCategory category) => _db.AssetCategories.Remove(category);
}
