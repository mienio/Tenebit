using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Settings;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetStatusSettingRepository : IAssetStatusSettingRepository
{
    private readonly TenebitDbContext _db;
    public AssetStatusSettingRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetStatusSetting>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.AssetStatusSettings.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);

    public Task<AssetStatusSetting?> GetByKeyAsync(Guid organizationId, string statusKey, CancellationToken cancellationToken) =>
        _db.AssetStatusSettings.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.StatusKey == statusKey, cancellationToken);

    public void Add(AssetStatusSetting setting) => _db.AssetStatusSettings.Add(setting);
}
