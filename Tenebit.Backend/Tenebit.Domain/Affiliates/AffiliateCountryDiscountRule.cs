using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

/// <summary>
/// Per-country discount applied automatically when a customer checks out through an
/// <see cref="AffiliateCode"/> whose own <see cref="AffiliateCode.CountryCode"/> matches their billing
/// country (spec §5.3/§13.2 - the "x miesiecy to x znizki" rule from the brief). Not wired into
/// checkout yet in this phase (see the Faza 2+ Paddle integration item in PLAN.md) - the table exists
/// now so a later phase never needs a schema migration against live discount data.
/// </summary>
public sealed class AffiliateCountryDiscountRule
{
    private AffiliateCountryDiscountRule() { }

    public AffiliateCountryDiscountRule(string countryCode, decimal discountPercent, int? durationMonths, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Trim().Length != 2)
            throw new DomainException("Kod kraju musi być dwuliterowym kodem ISO 3166-1.");
        if (discountPercent <= 0 || discountPercent > 100)
            throw new DomainException("Zniżka procentowa musi być w zakresie 1-100.");
        if (durationMonths is <= 0)
            throw new DomainException("Liczba miesięcy zniżki musi być większa od zera, jeśli podana.");

        Id = Guid.NewGuid();
        CountryCode = countryCode.Trim().ToUpperInvariant();
        DiscountPercent = discountPercent;
        DurationMonths = durationMonths;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string CountryCode { get; private set; } = string.Empty;
    public decimal DiscountPercent { get; private set; }

    /// <summary>Null = applies for the customer's whole subscription lifetime, same convention as
    /// <see cref="AffiliateProgramSettings.DefaultCommissionWindowMonths"/>.</summary>
    public int? DurationMonths { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(decimal discountPercent, int? durationMonths)
    {
        if (discountPercent <= 0 || discountPercent > 100)
            throw new DomainException("Zniżka procentowa musi być w zakresie 1-100.");
        if (durationMonths is <= 0)
            throw new DomainException("Liczba miesięcy zniżki musi być większa od zera, jeśli podana.");
        DiscountPercent = discountPercent;
        DurationMonths = durationMonths;
    }
}
