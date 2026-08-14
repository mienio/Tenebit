namespace Tenebit.Domain.Assets;

public enum AssetStatus
{
    Draft = 0,
    InStock = 1,
    Reserved = 2,
    Assigned = 3,
    InTransit = 4,
    InService = 5,
    Damaged = 6,
    Lost = 7,
    Retired = 8,
    Disposed = 9,
    PendingReturn = 10
}
