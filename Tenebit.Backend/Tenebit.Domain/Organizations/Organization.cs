using Tenebit.Domain.Common;

namespace Tenebit.Domain.Organizations;

public sealed class Organization
{
    private Organization() { }

    public Organization(string name, string country, string language, string currency, string timeZone, string? logoUrl = null)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdateProfile(name, country, language, currency, timeZone, logoUrl);
    }


    public static Organization CreateSeed(Guid id, string name, string country, string language, string currency, string timeZone, string? logoUrl = null)
    {
        var organization = new Organization(name, country, language, currency, timeZone, logoUrl);
        organization.Id = id;
        return organization;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Country { get; private set; } = "PL";
    public string Language { get; private set; } = "pl";
    public string Currency { get; private set; } = "PLN";
    public string TimeZone { get; private set; } = "Europe/Warsaw";
    public string? LogoUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdateProfile(string name, string country, string language, string currency, string timeZone, string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa firmy jest wymagana.");
        }

        Name = name.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? "PL" : country.Trim().ToUpperInvariant();
        Language = string.IsNullOrWhiteSpace(language) ? "pl" : language.Trim().ToLowerInvariant();
        Currency = string.IsNullOrWhiteSpace(currency) ? "PLN" : currency.Trim().ToUpperInvariant();
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "Europe/Warsaw" : timeZone.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
    }
}
