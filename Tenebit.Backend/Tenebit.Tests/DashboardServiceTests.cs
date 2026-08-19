using Tenebit.Application.Dashboard;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Dashboards;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class DashboardServiceTests
{
    private static (DashboardService Service, InMemoryAssetRepository Assets, InMemoryDashboardSnapshotRepository Snapshots, FakeClock Clock, FakeCurrentUser CurrentUser) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var snapshots = new InMemoryDashboardSnapshotRepository();
        var clock = new FakeClock();
        var service = new DashboardService(
            new InMemoryDashboardReadRepository(assets),
            new InMemoryActivityLogRepository(),
            new InMemoryDashboardLayoutRepository(),
            snapshots,
            currentUser,
            clock,
            new FakeUnitOfWork());
        return (service, assets, snapshots, clock, currentUser);
    }

    [Fact]
    public async Task GetSummaryAsync_RejectsEmployeeRole()
    {
        var (service, _, _, _, currentUser) = CreateService();
        currentUser.Roles = ["employee"];

        var result = await service.GetSummaryAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetSummaryAsync_RejectsManagerRole()
    {
        var (service, _, _, _, currentUser) = CreateService();
        currentUser.Roles = ["manager"];

        var result = await service.GetSummaryAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetComparisonAsync_ReturnsNotFound_WhenNoSnapshotExistsYet()
    {
        var (service, _, _, clock, _) = CreateService();
        clock.UtcNow = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

        var result = await service.GetComparisonAsync(7, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetComparisonAsync_ComparesCurrentStateAgainstClosestPriorSnapshot()
    {
        var (service, assets, snapshots, clock, currentUser) = CreateService();
        clock.UtcNow = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

        snapshots.Add(new DashboardSnapshot(currentUser.OrganizationId, new DateOnly(2026, 1, 8), totalAssets: 5, assetsWithoutOwner: 2, openAssignments: 1, visibleAssetValue: 1000m, createdAt: clock.UtcNow.AddDays(-7)));

        var categoryId = Guid.NewGuid();
        var asset = new Asset(currentUser.OrganizationId, categoryId, "Laptop", "TAG-1");
        asset.UpdateCore("Laptop", "TAG-1", null, categoryId, null, null, null, 500m, "PLN", null, null, null);
        assets.Assets.Add(asset);

        var result = await service.GetComparisonAsync(7, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 1, 8), result.Value!.ComparedToDate);
        Assert.Equal(1, result.Value!.CurrentTotalAssets);
        Assert.Equal(5, result.Value!.PreviousTotalAssets);
        Assert.Equal(1000m, result.Value!.PreviousVisibleAssetValue);
        Assert.Equal(500m, result.Value!.CurrentVisibleAssetValue);
    }

    [Fact]
    public async Task GetComparisonAsync_UsesClosestAvailableSnapshot_WhenExactDayIsMissing()
    {
        var (service, _, snapshots, clock, currentUser) = CreateService();
        clock.UtcNow = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

        // No snapshot for exactly 7 days ago (2026-01-08) - closest earlier one (2026-01-05) should be used instead.
        snapshots.Add(new DashboardSnapshot(currentUser.OrganizationId, new DateOnly(2026, 1, 5), totalAssets: 3, assetsWithoutOwner: 0, openAssignments: 0, visibleAssetValue: 0m, createdAt: clock.UtcNow.AddDays(-10)));

        var result = await service.GetComparisonAsync(7, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 1, 5), result.Value!.ComparedToDate);
    }
}
