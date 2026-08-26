using Tenebit.Domain.Assets;

namespace Tenebit.Application.Abstractions.Repositories;

public interface IPublicReportThrottleRepository
{
    /// <summary>Has this reporter already reported this asset inside the window?</summary>
    Task<bool> ExistsForReporterAndAssetAsync(Guid organizationId, Guid assetId, string reporterHash, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Reports on one asset from anyone - the limit that protects the admins' inbox.</summary>
    Task<int> CountForAssetAsync(Guid organizationId, Guid assetId, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Reports from one reporter across all assets - stops someone scanning a whole floor.</summary>
    Task<int> CountForReporterAsync(Guid organizationId, string reporterHash, DateTimeOffset since, CancellationToken cancellationToken);

    void Add(PublicReportThrottle entry);

    /// <summary>Drops rows past every window; the table only ever needs the recent past.</summary>
    Task PurgeOlderThanAsync(Guid organizationId, DateTimeOffset cutoff, CancellationToken cancellationToken);
}
