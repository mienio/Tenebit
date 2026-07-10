using Tenebit.Domain.Assets;

namespace Tenebit.Application.Dashboard;

public sealed record DashboardSummaryResponse(
    int TotalAssets,
    int AssetsInStock,
    int AssetsAssigned,
    int AssetsInService,
    int AssetsWithoutOwner,
    int PeopleCount,
    int OpenAssignments,
    int PendingProcedureAcceptances,
    decimal VisibleAssetValue,
    IReadOnlyList<StatusCountResponse> AssetsByStatus,
    IReadOnlyList<UpcomingAssetDateResponse> WarrantyExpiringSoon,
    IReadOnlyList<RecentActivityResponse> RecentActivity,
    IReadOnlyList<CategoryCountResponse> AssetsByCategory,
    IReadOnlyList<LocationCountResponse> AssetsByLocation,
    IReadOnlyList<TeamCountResponse> AssetsByTeam);

public sealed record StatusCountResponse(AssetStatus Status, int Count);
public sealed record UpcomingAssetDateResponse(Guid AssetId, string Name, string AssetTag, DateOnly WarrantyUntil);
public sealed record RecentActivityResponse(string Action, string EntityType, Guid? EntityId, string? DisplayName, string Actor, DateTimeOffset CreatedAt);
public sealed record CategoryCountResponse(Guid CategoryId, string CategoryName, int Count);
public sealed record LocationCountResponse(string Location, int Count);
public sealed record TeamCountResponse(Guid? TeamId, string TeamName, int Count, decimal TotalValue);
