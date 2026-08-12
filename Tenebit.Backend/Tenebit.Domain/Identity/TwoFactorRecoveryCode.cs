namespace Tenebit.Domain.Identity;

public sealed class TwoFactorRecoveryCode
{
    private TwoFactorRecoveryCode() { }

    public TwoFactorRecoveryCode(Guid organizationUserId, string codeHash, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        OrganizationUserId = organizationUserId;
        CodeHash = codeHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationUserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsUnused => UsedAt is null;

    public void MarkUsed(DateTimeOffset usedAt) => UsedAt = usedAt;
}
