using System.Text.RegularExpressions;
using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

/// <summary>
/// A single trackable code/link an affiliate promotes. Uniqueness spans the whole platform (a global
/// unique index at the persistence layer, plus a cross-check against
/// <see cref="Tenebit.Domain.Subscriptions.PromoCode.Code"/> in the application layer - the two share
/// one namespace of strings a customer can type into the "promo code" box at checkout) - never just
/// per-affiliate. Codes never expire (explicit product requirement): deactivating one frees a slot in
/// the affiliate's active-code limit but keeps its history (clicks/conversions already recorded)
/// intact.
/// </summary>
public sealed class AffiliateCode
{
    private static readonly Regex CodePattern = new("^[A-Z0-9][A-Z0-9-]{6,19}$", RegexOptions.Compiled);

    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMIN", "TENEBIT", "PARTNER", "AFFILIATE", "TENEB", "FREE", "STARTER", "GROWTH", "SCALE", "ENTERPRISE"
    };

    private AffiliateCode() { }

    public AffiliateCode(Guid affiliateId, string code, string? countryCode, DateTimeOffset now)
    {
        AffiliateId = affiliateId;
        Id = Guid.NewGuid();
        Code = Normalize(code);
        CountryCode = NormalizeCountryCode(countryCode);
        IsActive = true;
        ClickCount = 0;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid AffiliateId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? CountryCode { get; private set; }
    public bool IsActive { get; private set; }
    public int ClickCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordClick() => ClickCount++;

    public void SetActive(bool active) => IsActive = active;

    /// <summary>Format/blacklist validation only - the caller is responsible for the actual uniqueness
    /// check (both the DB unique index and the cross-check against PromoCode), since that requires a
    /// query this static helper cannot perform.</summary>
    public static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Kod afiliacyjny nie może być pusty.");
        var normalized = code.Trim().ToUpperInvariant();
        if (!CodePattern.IsMatch(normalized))
            throw new DomainException("Kod może zawierać 7-20 wielkich liter/cyfr/myślników, bez spacji i bez znaków specjalnych.");
        if (ReservedWords.Any(reserved => normalized.Contains(reserved, StringComparison.Ordinal)))
            throw new DomainException("Ten kod jest zastrzeżony. Wybierz inny.");
        return normalized;
    }

    private static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        var normalized = countryCode.Trim().ToUpperInvariant();
        if (normalized.Length != 2) throw new DomainException("Kod kraju musi być dwuliterowym kodem ISO 3166-1.");
        return normalized;
    }
}
