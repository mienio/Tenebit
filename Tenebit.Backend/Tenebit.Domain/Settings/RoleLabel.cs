namespace Tenebit.Domain.Settings;

// A per-organization override of a role's display label. Absence of a row means "use the built-in
// default label" from TenebitRoles.All - organizations only need a row once they rename a role.
public sealed class RoleLabel
{
    private RoleLabel() { }

    public RoleLabel(Guid organizationId, string roleKey, string label)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        RoleKey = roleKey;
        Label = label;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string RoleKey { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;

    public void SetLabel(string label) => Label = label;
}
