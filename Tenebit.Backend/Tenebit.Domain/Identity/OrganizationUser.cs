using Tenebit.Domain.Common;

namespace Tenebit.Domain.Identity;

public sealed class OrganizationUser
{
    private OrganizationUser() { }

    public OrganizationUser(Guid organizationId, string email, string displayName, bool isActive)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        SecurityStamp = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        Update(email, displayName, isActive, Enumerable.Empty<string>());
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? PersonId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public string? PasswordHash { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public string? TotpSecret { get; private set; }
    public bool IsTwoFactorEnabled { get; private set; }
    public long? LastUsedTotpCounter { get; private set; }
    public Guid SecurityStamp { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<OrganizationUserRole> Roles { get; private set; } = [];

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;
    public void RenameDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("Imię i nazwisko jest wymagane.");
        DisplayName = displayName.Trim();
    }
    public void LinkPerson(Guid? personId) => PersonId = personId;
    public void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();
    public void MarkEmailVerified() => IsEmailVerified = true;
    public void SetPendingTotpSecret(string secret) => TotpSecret = secret;
    public void EnableTwoFactor() => IsTwoFactorEnabled = true;
    public void RecordTotpCounter(long counter)
    {
        if (LastUsedTotpCounter.HasValue && counter <= LastUsedTotpCounter.Value)
            throw new DomainException("Kod uwierzytelniający został już użyty.");
        LastUsedTotpCounter = counter;
    }
    public void DisableTwoFactor()
    {
        IsTwoFactorEnabled = false;
        TotpSecret = null;
        LastUsedTotpCounter = null;
    }

    public void Update(string email, string displayName, bool isActive, IEnumerable<string> roles)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal)) throw new DomainException("Poprawny e-mail użytkownika jest wymagany.");
        Email = email.Trim().ToLowerInvariant();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Email : displayName.Trim();
        IsActive = isActive;
        Roles.Clear();
        foreach (var role in roles.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Roles.Add(new OrganizationUserRole(Id, role));
        }
    }
}
