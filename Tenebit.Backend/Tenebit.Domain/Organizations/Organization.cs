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
    public bool QrLabelShowSerialNumber { get; private set; }
    public bool QrLabelShowOrganizationName { get; private set; }
    public string? QrLabelCustomText { get; private set; }
    public QrLabelLogoMode QrLabelLogo { get; private set; } = QrLabelLogoMode.None;
    public QrLabelCodeSize QrLabelCodeSize { get; private set; } = QrLabelCodeSize.Medium;
    public QrLabelFormat QrLabelFormat { get; private set; } = QrLabelFormat.Medium63;
    public byte[]? QrLabelLogoImage { get; private set; }
    public string? QrLabelLogoContentType { get; private set; }
    public bool HasCustomQrLabelLogo => QrLabelLogoImage is { Length: > 0 };

    public QrLabelAppearance QrLabelAppearance => new(
        QrLabelShowName,
        QrLabelShowTag,
        QrLabelShowSerialNumber,
        QrLabelShowOrganizationName,
        QrLabelCustomText,
        QrLabelLogo,
        QrLabelCodeSize,
        QrLabelFormat);

    // Platform-level moderation (terms-of-service enforcement), set only from the admin panel.
    // Suspension blocks every sign-in for the organization but never touches its data, so it is fully
    // reversible - deliberately the strongest action the panel can take.
    public bool IsSuspended { get; private set; }
    public DateTimeOffset? SuspendedAt { get; private set; }
    public string? SuspendedReason { get; private set; }

    public void Suspend(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Powód zawieszenia jest wymagany.");
        }

        IsSuspended = true;
        SuspendedAt = now;
        SuspendedReason = reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
    }

    public void Restore()
    {
        IsSuspended = false;
        SuspendedAt = null;
        SuspendedReason = null;
    }

    public const int QrLabelCustomTextMaxLength = 60;

    public void UpdateQrLabelSettings(bool showName, bool showTag, bool showSerialNumber, bool showOrganizationName, string? customText, QrLabelLogoMode logo, QrLabelCodeSize codeSize, QrLabelFormat format)
    {
        var trimmed = customText?.Trim();
        if (trimmed is { Length: > QrLabelCustomTextMaxLength })
        {
            throw new DomainException($"Tekst na etykiecie może mieć maksymalnie {QrLabelCustomTextMaxLength} znaków.");
        }

        // Selecting the custom mark without an uploaded image would print a label with a gap where the
        // logo should be, and the gap would only become visible after someone printed a sheet of them.
        if (logo == QrLabelLogoMode.Custom && !HasCustomQrLabelLogo)
        {
            throw new DomainException("Najpierw wgraj własne logo, aby użyć go na etykiecie.");
        }

        QrLabelShowName = showName;
        QrLabelShowTag = showTag;
        QrLabelShowSerialNumber = showSerialNumber;
        QrLabelShowOrganizationName = showOrganizationName;
        QrLabelCustomText = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        QrLabelLogo = logo;
        QrLabelCodeSize = codeSize;
        QrLabelFormat = format;
    }

    public void SetQrLabelLogo(byte[] content, string contentType)
    {
        if (content.Length == 0)
        {
            throw new DomainException("Plik logo jest pusty.");
        }

        QrLabelLogoImage = content;
        QrLabelLogoContentType = contentType;
        QrLabelLogo = QrLabelLogoMode.Custom;
    }

    public void ClearQrLabelLogo()
    {
        QrLabelLogoImage = null;
        QrLabelLogoContentType = null;
        if (QrLabelLogo == QrLabelLogoMode.Custom) QrLabelLogo = QrLabelLogoMode.None;
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
