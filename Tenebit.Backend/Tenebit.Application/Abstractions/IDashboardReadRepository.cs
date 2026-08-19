using Tenebit.Domain.Assets;

namespace Tenebit.Application.Abstractions;

public sealed record DashboardStatusCount(AssetStatus Status, int Count);
public sealed record DashboardWarrantyAsset(Guid AssetId, string Name, string AssetTag, DateOnly WarrantyUntil);
public sealed record DashboardCategoryCount(Guid CategoryId, string CategoryName, int Count);
public sealed record DashboardLocationCount(string Location, int Count);
public sealed record DashboardTeamCount(Guid TeamId, string TeamName, int Count, decimal TotalValue);

public sealed record DashboardReadModel(
    int TotalAssets,
    int AssetsInStock,
    int AssetsAssigned,
    int AssetsInService,
    int AssetsWithoutOwner,
    int PeopleCount,
    int OpenAssignments,
    int PendingProcedureAcceptances,
    decimal VisibleAssetValue,
    int TotalLicenses,
    int LicenseSeatsUsed,
    int LicenseSeatsTotal,
    IReadOnlyList<DashboardStatusCount> AssetsByStatus,
    IReadOnlyList<DashboardWarrantyAsset> WarrantyExpiringSoon,
    IReadOnlyList<DashboardCategoryCount> AssetsByCategory,
    IReadOnlyList<DashboardLocationCount> AssetsByLocation,
    IReadOnlyList<DashboardTeamCount> AssetsByTeam);

public sealed record DashboardSnapshotMetrics(
    int TotalAssets,
    int AssetsWithoutOwner,
    int OpenAssignments,
    decimal VisibleAssetValue);

public interface IDashboardReadRepository
{
    Task<DashboardReadModel> GetSummaryAsync(Guid organizationId, DateOnly today, DateOnly warrantyLimit, CancellationToken cancellationToken);
    Task<DashboardSnapshotMetrics> GetSnapshotMetricsAsync(Guid organizationId, CancellationToken cancellationToken);
}
