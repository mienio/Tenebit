using System.Text.RegularExpressions;
using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

public enum AffiliateStatus
{
    PendingApproval,
    Active,
    Blocked
}

/// <summary>
/// Aggregate root for a partner in the affiliate program - a completely separate identity from
/// <see cref="Tenebit.Domain.Identity.OrganizationUser"/> (a paying tenant) and the single platform
/// admin account. An affiliate never belongs to an Organization and is never granted tenant roles;
/// see <c>AffiliateClaims</c> in Tenebit.Api for the JWT scope that keeps the three identity kinds
/// mutually exclusive by construction.
/// </summary>
public sealed class Affiliate
{
    public const int DefaultMaxActiveCodes = 10;

    private static readonly Regex RevolutTagPattern = new("^@[A-Za-z0-9_.]{2,32}$", RegexOptions.Compiled);

    private Affiliate() { }

    public Affiliate(string email, string passwordHash, string firstName, string lastName, string? countryCode, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            throw new DomainException("Poprawny e-mail jest wymagany.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Hasło jest wymagane.");
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Imię i nazwisko są wymagane.");

        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        CountryCode = NormalizeCountryCode(countryCode);
        Status = AffiliateStatus.PendingApproval;
        SecurityStamp = Guid.NewGuid();
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public AffiliateStatus Status { get; private set; }
    public string? CountryCode { get; private set; }
    public string? PhoneNumber { get; private set; }

    /// <summary>Optional company name/tax id - the program does not require these at registration
    /// (see spec §16.5, left as an admin-configurable decision), but the field is reserved now so a
    /// later switch to invoiced B2B commissions never needs a migration against existing financial
    /// records.</summary>
    public string? CompanyName { get; private set; }
    public string? TaxId { get; private set; }

    /// <summary>Revolut payment tag ("@handle") the owner transfers commission to by hand each payout
    /// cycle - encrypted at rest via an EF Core value converter (see
    /// FieldEncryptionPurposes.AffiliateRevolutTag), same pattern as the TOTP secret. Optional at
    /// registration; the affiliate can add or change it later from their own profile.</summary>
    public string? RevolutTag { get; private set; }

    /// <summary>Null = use <see cref="Tenebit.Domain.Affiliates.AffiliateProgramSettings.DefaultCommissionPercent"/>.</summary>
    public decimal? CommissionPercentOverride { get; private set; }

    /// <summary>Null = use <see cref="Tenebit.Domain.Affiliates.AffiliateProgramSettings.DefaultMaxCodesPerAffiliate"/>.</summary>
    public int? MaxActiveCodesOverride { get; private set; }

    public bool IsEmailVerified { get; private set; }
    public Guid SecurityStamp { get; private set; }

    public DateTimeOffset? AcceptedTermsAt { get; private set; }
    public string? AcceptedTermsVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? BlockedAt { get; private set; }
    public string? BlockedReason { get; private set; }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Hasło jest wymagane.");
        PasswordHash = passwordHash;
        RotateSecurityStamp();
    }

    public void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();

    public void MarkEmailVerified() => IsEmailVerified = true;

    public void AcceptTerms(string version, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new DomainException("Wersja regulaminu jest wymagana.");
        AcceptedTermsAt = now;
        AcceptedTermsVersion = version;
    }

    public void UpdateContactDetails(string firstName, string lastName, string? phoneNumber, string? countryCode, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Imię i nazwisko są wymagane.");
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        CountryCode = NormalizeCountryCode(countryCode);
        UpdatedAt = now;
    }

    public void UpdateCompanyDetails(string? companyName, string? taxId, DateTimeOffset now)
    {
        CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        TaxId = string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim().ToUpperInvariant();
        UpdatedAt = now;
    }

    /// <summary>Revolut tags start with "@" (e.g. "@damian.kowalski") - validated so the admin never
    /// stares at an unusable value when it's time to actually send the money. Passing null/empty
    /// clears the field - the spec explicitly allows leaving it blank at registration.</summary>
    public void SetRevolutTag(string? revolutTag, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(revolutTag))
        {
            RevolutTag = null;
        }
        else
        {
            var trimmed = revolutTag.Trim();
            if (!RevolutTagPattern.IsMatch(trimmed))
                throw new DomainException("Revtag Revolut musi mieć postać @nazwa (2-32 znaki: litery, cyfry, kropka, podkreślnik).");
            RevolutTag = trimmed;
        }
        UpdatedAt = now;
    }

    public void Approve(DateTimeOffset now)
    {
        if (Status == AffiliateStatus.Blocked)
            throw new DomainException("Konto zablokowane - najpierw je odblokuj (Reactivate), zanim zatwierdzisz ponownie.");
        Status = AffiliateStatus.Active;
        ApprovedAt = now;
        UpdatedAt = now;
    }

    public void Block(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Powód blokady jest wymagany.");
        Status = AffiliateStatus.Blocked;
        BlockedAt = now;
        BlockedReason = reason.Trim();
        RotateSecurityStamp();
        UpdatedAt = now;
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (Status != AffiliateStatus.Blocked)
            throw new DomainException("Można reaktywować tylko zablokowane konto.");
        Status = AffiliateStatus.Active;
        BlockedAt = null;
        BlockedReason = null;
        UpdatedAt = now;
    }

    public void OverrideCommissionPercent(decimal? percent)
    {
        if (percent is < 0 or > 100) throw new DomainException("Prowizja musi być w zakresie 0-100%.");
        CommissionPercentOverride = percent;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void OverrideMaxActiveCodes(int? maxActiveCodes)
    {
        if (maxActiveCodes is <= 0) throw new DomainException("Limit aktywnych kodów musi być większy od zera.");
        MaxActiveCodesOverride = maxActiveCodes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public decimal ResolveCommissionPercent(AffiliateProgramSettings settings) =>
        CommissionPercentOverride ?? settings.DefaultCommissionPercent;

    public int ResolveMaxActiveCodes(AffiliateProgramSettings settings) =>
        MaxActiveCodesOverride ?? settings.DefaultMaxCodesPerAffiliate;

    private static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        var normalized = countryCode.Trim().ToUpperInvariant();
        if (normalized.Length != 2) throw new DomainException("Kod kraju musi być dwuliterowym kodem ISO 3166-1.");
        return normalized;
    }
}
