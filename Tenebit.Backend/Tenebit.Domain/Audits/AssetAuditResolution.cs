namespace Tenebit.Domain.Audits;

public enum AssetAuditResolution
{
    None,
    Accepted,
    AssetMarkedLost,
    AssetMarkedDamaged,
    OwnershipCorrected,
    Dismissed
}
