using Tenebit.Domain.Common;

namespace Tenebit.Domain.Dashboards;

public sealed class DashboardLayout
{
    private DashboardLayout() { }

    public DashboardLayout(Guid organizationId, Guid organizationUserId, string layoutJson)
    {
        OrganizationId = organizationId;
        OrganizationUserId = organizationUserId;
        Update(layoutJson);
    }

    public Guid OrganizationUserId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string LayoutJson { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson)) throw new DomainException("Układ pulpitu nie może być pusty.");
        LayoutJson = layoutJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
