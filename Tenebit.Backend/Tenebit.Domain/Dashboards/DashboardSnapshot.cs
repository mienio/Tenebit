namespace Tenebit.Domain.Dashboards;

public sealed class DashboardSnapshot
{
    private DashboardSnapshot() { }

    public DashboardSnapshot(Guid organizationId, DateOnly snapshotDate, int totalAssets, int assetsWithoutOwner, int openAssignments, decimal visibleAssetValue, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        SnapshotDate = snapshotDate;
        TotalAssets = totalAssets;
        AssetsWithoutOwner = assetsWithoutOwner;
        OpenAssignments = openAssignments;
        VisibleAssetValue = visibleAssetValue;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public DateOnly SnapshotDate { get; private set; }
    public int TotalAssets { get; private set; }
    public int AssetsWithoutOwner { get; private set; }
    public int OpenAssignments { get; private set; }
    public decimal VisibleAssetValue { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
