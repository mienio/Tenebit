namespace Tenebit.Domain.Licenses;

public sealed class LicenseSeat
{
    private LicenseSeat() { }

    public LicenseSeat(Guid organizationId, Guid licenseId, Guid personId, DateTimeOffset assignedAt)
    {
        OrganizationId = organizationId;
        LicenseId = licenseId;
        PersonId = personId;
        AssignedAt = assignedAt;
    }

    public Guid OrganizationId { get; private set; }
    public Guid LicenseId { get; private set; }
    public Guid PersonId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
}
