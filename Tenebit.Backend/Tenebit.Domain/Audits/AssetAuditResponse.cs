namespace Tenebit.Domain.Audits;

public enum AssetAuditResponse
{
    Pending,
    Confirmed,
    Missing,
    Damaged,
    WrongOwner
}
