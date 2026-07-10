namespace Tenebit.Domain.Identity;

public sealed class OrganizationUserRole
{
    private OrganizationUserRole() { }
    public OrganizationUserRole(Guid userId, string role)
    {
        UserId = userId;
        Role = role;
    }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;
}
