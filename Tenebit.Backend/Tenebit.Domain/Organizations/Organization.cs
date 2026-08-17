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
    public TimeOnly? QuietHoursStart { get; private set; }
    public TimeOnly? QuietHoursEnd { get; private set; }
    public PublicIpCaptureMode CapturePublicIp { get; private set; } = PublicIpCaptureMode.Off;
    public int? PublicIpRetentionDays { get; private set; }
    public int? DefaultEvidenceRetentionMonths { get; private set; }
    public string? PrivacyNoticeUrl { get; private set; }
    public string? PrivacyContactEmail { get; private set; }
    public bool QrLabelShowName { get; private set; } = true;
    public bool QrLabelShowTag { get; private set; } = true;

    public void UpdateQrLabelSettings(bool showName, bool showTag)
    {
        QrLabelShowName = showName;
        QrLabelShowTag = showTag;
    }

    public void UpdatePrivacySettings(PublicIpCaptureMode capturePublicIp, int? publicIpRetentionDays, int? defaultEvidenceRetentionMonths, string? privacyNoticeUrl, string? privacyContactEmail)
    {
        if (capturePublicIp != PublicIpCaptureMode.Off && (!publicIpRetentionDays.HasValue || publicIpRetentionDays.Value <= 0))
        {
            throw new DomainException("Okres przechowywania adresu IP jest wymagany, gdy przechwytywanie adresu IP jest włączone.");
        }

        if (defaultEvidenceRetentionMonths.HasValue && defaultEvidenceRetentionMonths.Value <= 0)
        {
            throw new DomainException("Okres przechowywania materiału dowodowego musi być większy od zera.");
        }

        CapturePublicIp = capturePublicIp;
        PublicIpRetentionDays = capturePublicIp == PublicIpCaptureMode.Off ? null : publicIpRetentionDays;
        DefaultEvidenceRetentionMonths = defaultEvidenceRetentionMonths;
        PrivacyNoticeUrl = string.IsNullOrWhiteSpace(privacyNoticeUrl) ? null : privacyNoticeUrl.Trim();
        PrivacyContactEmail = string.IsNullOrWhiteSpace(privacyContactEmail) ? null : privacyContactEmail.Trim();
    }

    public void SetQuietHours(TimeOnly? start, TimeOnly? end)
    {
        if (start.HasValue != end.HasValue)
        {
            throw new DomainException("Godziny ciszy wymagają obu wartości: początku i końca.");
        }

        QuietHoursStart = start;
        QuietHoursEnd = end;
    }

    public bool IsWithinQuietHours(DateTimeOffset utcNow)
    {
        if (QuietHoursStart is null || QuietHoursEnd is null || QuietHoursStart == QuietHoursEnd) return false;

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }

        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, timeZone).DateTime);
        var start = QuietHoursStart.Value;
        var end = QuietHoursEnd.Value;
        return start < end ? (localTime >= start && localTime < end) : (localTime >= start || localTime < end);
    }

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
