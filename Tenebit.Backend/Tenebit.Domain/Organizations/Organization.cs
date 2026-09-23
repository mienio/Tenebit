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

    // Dane nabywcy drukowane na fakturze wystawianej przez Paddle (Merchant of Record). Trzymamy je u
    // siebie, a nie tylko w Paddle, z dwóch powodów: kupujący musi móc je sprawdzić i poprawić w
    // ustawieniach jeszcze przed zapłatą (podgląd faktury na /pricing), a checkout ma je podstawić
    // zamiast kazać firmie przepisywać NIP w okienku Paddle.
    public string? BillingCompanyName { get; private set; }

    /// <summary>Numer VAT nabywcy - w Polsce NIP, w UE numer VAT UE. Zawsze przechowywany w postaci
    /// znormalizowanej (bez spacji i myślników, wielkimi literami), bo tylko taką przyjmuje Paddle i tylko
    /// taką da się porównać z tym, co wróci z jego API.</summary>
    public string? TaxId { get; private set; }
    public string? BillingAddressLine1 { get; private set; }
    public string? BillingAddressLine2 { get; private set; }
    public string? BillingCity { get; private set; }
    public string? BillingPostalCode { get; private set; }
    public string? BillingCountry { get; private set; }

    /// <summary>Nazwa nabywcy na fakturze - dane rozliczeniowe, a w ich braku nazwa organizacji.</summary>
    public string InvoiceName => string.IsNullOrWhiteSpace(BillingCompanyName) ? Name : BillingCompanyName;

    /// <summary>Kraj nabywcy na fakturze (ISO 3166-1 alpha-2) - z danych rozliczeniowych, a w ich braku
    /// kraj organizacji.</summary>
    public string InvoiceCountry => string.IsNullOrWhiteSpace(BillingCountry) ? Country : BillingCountry;

    /// <summary>Komplet danych, których Paddle wymaga do wystawienia faktury na firmę: nazwa, numer VAT
    /// oraz adres (Paddle wymaga co najmniej kraju i kodu pocztowego).</summary>
    public bool HasCompleteBillingDetails =>
        !string.IsNullOrWhiteSpace(TaxId) &&
        !string.IsNullOrWhiteSpace(BillingAddressLine1) &&
        !string.IsNullOrWhiteSpace(BillingCity) &&
        !string.IsNullOrWhiteSpace(BillingPostalCode) &&
        !string.IsNullOrWhiteSpace(InvoiceCountry);

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

    public const int TaxIdMaxLength = 20;

    /// <summary>Kraje, w których numer VAT zaczyna się od dwuliterowego prefiksu - tam sam ciąg cyfr (np.
    /// polski NIP przepisany z pieczątki) jest poprawnym numerem dopiero po dopisaniu prefiksu, a bez
    /// niego Paddle odrzuca go jako nieprawidłowy. Grecja jest wyjątkiem: kod kraju to GR, a prefiks VAT
    /// to EL.</summary>
    private static readonly Dictionary<string, string> VatPrefixByCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AT"] = "ATU", ["BE"] = "BE", ["BG"] = "BG", ["HR"] = "HR", ["CY"] = "CY", ["CZ"] = "CZ",
        ["DK"] = "DK", ["EE"] = "EE", ["FI"] = "FI", ["FR"] = "FR", ["DE"] = "DE", ["GR"] = "EL",
        ["HU"] = "HU", ["IE"] = "IE", ["IT"] = "IT", ["LV"] = "LV", ["LT"] = "LT", ["LU"] = "LU",
        ["MT"] = "MT", ["NL"] = "NL", ["PL"] = "PL", ["PT"] = "PT", ["RO"] = "RO", ["SK"] = "SK",
        ["SI"] = "SI", ["ES"] = "ES", ["SE"] = "SE"
    };

    /// <summary>Sprowadza wpisany numer VAT do postaci, jakiej oczekuje Paddle: bez separatorów, wielkimi
    /// literami i - dla krajów UE - z prefiksem kraju, jeśli użytkownik wpisał same cyfry.</summary>
    public static string? NormalizeTaxId(string? raw, string? country)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var compact = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (compact.Length == 0) return null;

        if (compact.All(char.IsDigit)
            && !string.IsNullOrWhiteSpace(country)
            && VatPrefixByCountry.TryGetValue(country.Trim(), out var prefix))
        {
            compact = prefix + compact;
        }

        if (compact.Length < 5 || compact.Length > TaxIdMaxLength)
        {
            throw new DomainException("VAT ID ma nieprawidłową długość.");
        }

        return compact;
    }

    /// <summary>Dane nabywcy na fakturę. Wszystko jest opcjonalne - dopóki organizacja nie kupuje płatnego
    /// planu, nie ma powodu ich wymagać; kompletności pilnuje dopiero checkout
    /// (<see cref="HasCompleteBillingDetails"/>).</summary>
    public void UpdateBillingDetails(string? companyName, string? taxId, string? addressLine1, string? addressLine2, string? city, string? postalCode, string? country)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant();
        if (normalizedCountry is { Length: not 2 })
        {
            throw new DomainException("Kraj do faktury podaj jako dwuliterowy kod ISO, np. PL.");
        }

        BillingCompanyName = Trimmed(companyName, 200);
        TaxId = NormalizeTaxId(taxId, normalizedCountry ?? Country);
        BillingAddressLine1 = Trimmed(addressLine1, 200);
        BillingAddressLine2 = Trimmed(addressLine2, 200);
        BillingCity = Trimmed(city, 120);
        BillingPostalCode = Trimmed(postalCode, 20);
        BillingCountry = normalizedCountry;
    }

    private static string? Trimmed(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
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
