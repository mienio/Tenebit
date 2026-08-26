using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions.Repositories;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class PublicReportThrottleRepository : IPublicReportThrottleRepository
{
    private readonly TenebitDbContext _db;

    public PublicReportThrottleRepository(TenebitDbContext db) => _db = db;

    public Task<bool> ExistsForReporterAndAssetAsync(Guid organizationId, Guid assetId, string reporterHash, DateTimeOffset since, CancellationToken cancellationToken) =>
        _db.PublicReportThrottles
            .AnyAsync(x => x.OrganizationId == organizationId && x.AssetId == assetId && x.ReporterHash == reporterHash && x.CreatedAt >= since, cancellationToken);

    public Task<int> CountForAssetAsync(Guid organizationId, Guid assetId, DateTimeOffset since, CancellationToken cancellationToken) =>
        _db.PublicReportThrottles
            .CountAsync(x => x.OrganizationId == organizationId && x.AssetId == assetId && x.CreatedAt >= since, cancellationToken);

    public Task<int> CountForReporterAsync(Guid organizationId, string reporterHash, DateTimeOffset since, CancellationToken cancellationToken) =>
        _db.PublicReportThrottles
            .CountAsync(x => x.OrganizationId == organizationId && x.ReporterHash == reporterHash && x.CreatedAt >= since, cancellationToken);

    public void Add(PublicReportThrottle entry) => _db.PublicReportThrottles.Add(entry);

    public Task PurgeOlderThanAsync(Guid organizationId, DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        _db.PublicReportThrottles
            .Where(x => x.OrganizationId == organizationId && x.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
}
