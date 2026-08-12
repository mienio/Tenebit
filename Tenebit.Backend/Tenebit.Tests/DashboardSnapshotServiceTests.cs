using Tenebit.Application.Dashboard;
using Tenebit.Domain.Organizations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class DashboardSnapshotServiceTests
{
    private static (DashboardSnapshotService Service, InMemoryOrganizationRepository Organizations, InMemoryAssetRepository Assets, InMemoryAssignmentRepository Assignments, InMemoryDashboardSnapshotRepository Snapshots, FakeClock Clock) CreateService()
    {
        var organizations = new InMemoryOrganizationRepository();
        var assets = new InMemoryAssetRepository();
        var assignments = new InMemoryAssignmentRepository();
        var snapshots = new InMemoryDashboardSnapshotRepository();
        var clock = new FakeClock();
        var service = new DashboardSnapshotService(organizations, assets, assignments, snapshots, clock, new FakeUnitOfWork());
        return (service, organizations, assets, assignments, snapshots, clock);
    }

    [Fact]
    public async Task CaptureAllOrganizationsAsync_CreatesOneSnapshotPerOrganizationForToday()
    {
        var (service, organizations, _, _, snapshots, clock) = CreateService();
        var orgA = new Organization("Acme", "PL", "pl", "PLN", "Europe/Warsaw");
        var orgB = new Organization("OtherCo", "PL", "pl", "PLN", "Europe/Warsaw");
        organizations.Add(orgA);
        organizations.Add(orgB);
        clock.UtcNow = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

        await service.CaptureAllOrganizationsAsync(CancellationToken.None);

        Assert.Equal(2, snapshots.Snapshots.Count);
        Assert.All(snapshots.Snapshots, s => Assert.Equal(new DateOnly(2026, 1, 15), s.SnapshotDate));
    }

    [Fact]
    public async Task CaptureAllOrganizationsAsync_DoesNotDuplicateSnapshotForSameDay()
    {
        var (service, organizations, _, _, snapshots, clock) = CreateService();
        var org = new Organization("Acme", "PL", "pl", "PLN", "Europe/Warsaw");
        organizations.Add(org);
        clock.UtcNow = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

        await service.CaptureAllOrganizationsAsync(CancellationToken.None);
        await service.CaptureAllOrganizationsAsync(CancellationToken.None);

        Assert.Single(snapshots.Snapshots);
    }

    [Fact]
    public async Task CaptureAllOrganizationsAsync_CountsAssetsWithoutOwner()
    {
        var (service, organizations, assets, _, snapshots, clock) = CreateService();
        var org = new Organization("Acme", "PL", "pl", "PLN", "Europe/Warsaw");
        organizations.Add(org);
        clock.UtcNow = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

        var categoryId = Guid.NewGuid();
        var unassigned = new Tenebit.Domain.Assets.Asset(org.Id, categoryId, "Laptop A", "TAG-1");
        var assigned = new Tenebit.Domain.Assets.Asset(org.Id, categoryId, "Laptop B", "TAG-2");
        assigned.AssignTo(Guid.NewGuid());
        assets.Assets.Add(unassigned);
        assets.Assets.Add(assigned);

        await service.CaptureAllOrganizationsAsync(CancellationToken.None);

        var snapshot = Assert.Single(snapshots.Snapshots);
        Assert.Equal(2, snapshot.TotalAssets);
        Assert.Equal(1, snapshot.AssetsWithoutOwner);
    }
}
