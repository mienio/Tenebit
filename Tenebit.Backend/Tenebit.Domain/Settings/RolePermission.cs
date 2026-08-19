namespace Tenebit.Domain.Settings;

// A per-organization (role, permission) override. Absence of a row means "use the built-in
// default" for that role+permission (see RolePermissions.DefaultAllow) - admins only need to
// create a row when they want to flip a role away from its default.
public sealed class RolePermission
{
    private RolePermission() { }

    public RolePermission(Guid organizationId, string roleKey, string permissionKey, bool allowed)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        RoleKey = roleKey;
        PermissionKey = permissionKey;
        Allowed = allowed;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string RoleKey { get; private set; } = string.Empty;
    public string PermissionKey { get; private set; } = string.Empty;
    public bool Allowed { get; private set; }

    public void SetAllowed(bool allowed) => Allowed = allowed;
}
