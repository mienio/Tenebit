namespace Tenebit.Domain.Identity;

public sealed class ExternalLogin
{
    private ExternalLogin() { }

    public ExternalLogin(Guid organizationUserId, string provider, string providerUserId)
    {
        Id = Guid.NewGuid();
        OrganizationUserId = organizationUserId;
        Provider = provider;
        ProviderUserId = providerUserId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationUserId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
