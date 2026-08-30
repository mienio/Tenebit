using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Organizations;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.Settings;

namespace Tenebit.Application.Settings;

public sealed class SettingsService
{
    private readonly IAssetStatusSettingRepository _statusSettings;
    private readonly IOrganizationRepository _organizations;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly IImageSanitizer _imageSanitizer;
    private readonly IAppLinkBuilder _linkBuilder;

    public SettingsService(IAssetStatusSettingRepository statusSettings, IOrganizationRepository organizations, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IQrCodeGenerator qrCodeGenerator, IImageSanitizer imageSanitizer, IAppLinkBuilder linkBuilder)
    {
        _statusSettings = statusSettings;
        _organizations = organizations;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _qrCodeGenerator = qrCodeGenerator;
        _imageSanitizer = imageSanitizer;
        _linkBuilder = linkBuilder;
    }

    public async Task<IReadOnlyList<AssetStatusSettingResponse>> ListAssetStatusesAsync(CancellationToken cancellationToken)
    {
        var saved = await _statusSettings.ListAsync(_currentUser.OrganizationId, cancellationToken);
        if (saved.Count == 0)
        {
            return BuiltInStatusSettings(_currentUser.Language);
        }

        return saved.OrderBy(x => x.SortOrder).Select(x => new AssetStatusSettingResponse(x.StatusKey, x.Label.Trim(), x.Color, x.BackgroundColor, x.SortOrder, x.IsEnabled)).ToList();
    }

    public async Task<Result<IReadOnlyList<AssetStatusSettingResponse>>> SaveAssetStatusesAsync(IReadOnlyList<SaveAssetStatusSettingRequest> request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<IReadOnlyList<AssetStatusSettingResponse>>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var allowed = Enum.GetNames<AssetStatus>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var item in request)
            {
                if (!allowed.Contains(item.StatusKey)) return Result<IReadOnlyList<AssetStatusSettingResponse>>.Failure(Error.Validation($"Nieznany status aktywa: {item.StatusKey}."));
                var setting = await _statusSettings.GetByKeyAsync(organizationId, item.StatusKey, cancellationToken);
                if (setting is null)
                {
                    setting = new AssetStatusSetting(organizationId, item.StatusKey, item.Label, item.Color, item.BackgroundColor, item.SortOrder, item.IsEnabled);
                    _statusSettings.Add(setting);
                }
                else
                {
                    setting.Update(item.Label, item.Color, item.BackgroundColor, item.SortOrder, item.IsEnabled);
                }
            }
            _activity.Add(new ActivityLog(organizationId, "settings.asset_statuses.updated", "settings", organizationId, _currentUser.Subject, null, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var statuses = await ListAssetStatusesAsync(cancellationToken);
            return Result<IReadOnlyList<AssetStatusSettingResponse>>.Success(statuses);
        }
        catch (DomainException ex) { return Result<IReadOnlyList<AssetStatusSettingResponse>>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<EvidencePrivacySettingsResponse>> GetEvidencePrivacyAsync(CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
        if (organization is null) return Result<EvidencePrivacySettingsResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));
        return Result<EvidencePrivacySettingsResponse>.Success(MapPrivacySettings(organization));
    }

    public async Task<Result<EvidencePrivacySettingsResponse>> SaveEvidencePrivacyAsync(SaveEvidencePrivacySettingsRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<EvidencePrivacySettingsResponse>.Failure(access.Error!);

        var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
        if (organization is null) return Result<EvidencePrivacySettingsResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));

        try
        {
            organization.UpdatePrivacySettings(request.CapturePublicIp, request.PublicIpRetentionDays, request.DefaultEvidenceRetentionMonths, request.PrivacyNoticeUrl, request.PrivacyContactEmail);
            _activity.Add(new ActivityLog(organization.Id, "settings.evidence_privacy.updated", "organization", organization.Id, _currentUser.Subject, null, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<EvidencePrivacySettingsResponse>.Success(MapPrivacySettings(organization));
        }
        catch (DomainException ex)
        {
            return Result<EvidencePrivacySettingsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private static EvidencePrivacySettingsResponse MapPrivacySettings(Tenebit.Domain.Organizations.Organization organization) =>
        new(organization.CapturePublicIp, organization.PublicIpRetentionDays, organization.DefaultEvidenceRetentionMonths, organization.PrivacyNoticeUrl, organization.PrivacyContactEmail);

    public async Task<Result<QrLabelSettingsResponse>> GetQrLabelSettingsAsync(CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
        if (organization is null) return Result<QrLabelSettingsResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));
        return Result<QrLabelSettingsResponse>.Success(ToQrLabelResponse(organization));
    }

    public async Task<Result<QrLabelSettingsResponse>> SaveQrLabelSettingsAsync(SaveQrLabelSettingsRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<QrLabelSettingsResponse>.Failure(access.Error!);

        var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
        if (organization is null) return Result<QrLabelSettingsResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));

        try
        {
            organization.UpdateQrLabelSettings(request.ShowName, request.ShowTag, request.ShowSerialNumber, request.ShowOrganizationName, request.CustomText, request.Logo, request.CodeSize, request.Format);
        }
        catch (DomainException ex)
        {
            return Result<QrLabelSettingsResponse>.Failure(Error.Validation(ex.Message));
        }

        _activity.Add(new ActivityLog(organization.Id, "settings.qr_label.updated", "organization", organization.Id, _currentUser.Subject, null, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<QrLabelSettingsResponse>.Success(ToQrLabelResponse(organization));
    }

    /// <summary>Largest logo accepted for a label. A printed mark is ~2 cm wide; anything past this is pixels nobody sees.</summary>
    public const int MaxQrLabelLogoBytes = 512 * 1024;

    public async Task<Result<QrLabelSettingsResponse>> UploadQrLabelLogoAsync(string? contentType, byte[] content, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<QrLabelSettingsResponse>.Failure(access.Error!);

        if (!ImageSignature.IsAllowedContentType(contentType))
        {
            return Result<QrLabelSettingsResponse>.Failure(Error.Validation("Dozwolone są tylko obrazy w formacie JPEG, PNG lub WebP."));
        }

        if (content.Length == 0) return Result<QrLabelSettingsResponse>.Failure(Error.Validation("Plik logo jest pusty."));
        if (content.Length > MaxQrLabelLogoBytes)
        {
            return Result<QrLabelSettingsResponse>.Failure(Error.Validation("Logo może mieć maksymalnie 512 KB."));
        }

        var format = ImageSignature.Detect(content);
        if (format == DetectedImageFormat.Unknown)
        {
            return Result<QrLabelSettingsResponse>.Failure(Error.Validation("Plik nie jest prawidłowym obrazem JPEG/PNG/WebP."));
        }

        var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
        if (organization is null) return Result<QrLabelSettingsResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));

        // Same stripping as evidence photos: the logo is embedded into every label and downloaded as an
        // image, so any metadata a designer's export carried would travel with it.
        var sanitized = _imageSanitizer.StripMetadata(format, content);
        organization.SetQrLabelLogo(sanitized.Content, sanitized.ContentType);
        _activity.Add(new ActivityLog(organization.Id, "settings.qr_label.logo_uploaded", "organization", organization.Id, _currentUser.Subject, null, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<QrLabelSettingsResponse>.Success(ToQrLabelResponse(organization));
    }

    public async Task<Result<QrLabelSettingsResponse>> RemoveQrLabelLogoAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<QrLabelSettingsResponse>.Failure(access.Error!);

        var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
        if (organization is null) return Result<QrLabelSettingsResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));

        organization.ClearQrLabelLogo();
        _activity.Add(new ActivityLog(organization.Id, "settings.qr_label.logo_removed", "organization", organization.Id, _currentUser.Subject, null, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<QrLabelSettingsResponse>.Success(ToQrLabelResponse(organization));
    }

    /// <summary>
    /// Renders a sample label from unsaved settings, so the editor shows the real output rather than an
    /// approximation of it. The draft is never applied to the tracked organization - only its stored logo
    /// is read - so closing the editor without saving leaves nothing behind.
    /// </summary>
    public async Task<Result<QrLabelPreviewResponse>> PreviewQrLabelAsync(SaveQrLabelSettingsRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<QrLabelPreviewResponse>.Failure(access.Error!);

        var organization = await _organizations.GetAsync(_currentUser.OrganizationId, cancellationToken);
        if (organization is null) return Result<QrLabelPreviewResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));

        var customText = request.CustomText?.Trim();
        if (customText is { Length: > Organization.QrLabelCustomTextMaxLength })
        {
            return Result<QrLabelPreviewResponse>.Failure(Error.Validation($"Tekst na etykiecie może mieć maksymalnie {Organization.QrLabelCustomTextMaxLength} znaków."));
        }

        var logo = request.Logo == QrLabelLogoMode.Custom && !organization.HasCustomQrLabelLogo ? QrLabelLogoMode.None : request.Logo;
        var appearance = new QrLabelAppearance(request.ShowName, request.ShowTag, request.ShowSerialNumber, request.ShowOrganizationName, customText, logo, request.CodeSize, request.Format);
        var content = QrLabelComposer.Compose(
            appearance,
            organization.Name,
            organization.QrLabelLogoImage,
            organization.QrLabelLogoContentType,
            SampleAssetName,
            SampleAssetTag,
            SampleSerialNumber);

        // Sample payload is built from real identifiers so the preview shows the density a printed label
        // will actually have. A shorter placeholder would quietly promise a code that scans better than
        // the real one.
        var render = _qrCodeGenerator.RenderAssetQrLabel(_linkBuilder.BuildAssetScanLink(SampleScanCode), content);
        var (labelWidthMm, labelHeightMm) = QrLabelComposer.FormatMillimetres(request.Format);

        // The sheet prints each label with `object-fit: contain` inside a padded cell, so the limiting
        // dimension decides the scale - the same arithmetic, done here so both sides cannot drift.
        var innerWidth = labelWidthMm - 2 * LabelPaddingMm;
        var innerHeight = labelHeightMm - 2 * LabelPaddingMm;
        var scale = Math.Min(innerWidth / render.WidthPx, innerHeight / render.HeightPx);
        var codeMm = render.CodeSizePx * scale;

        return Result<QrLabelPreviewResponse>.Success(new QrLabelPreviewResponse(
            render.Svg,
            render.WidthPx,
            render.HeightPx,
            render.CodeSizePx,
            render.ModuleCount,
            labelWidthMm,
            labelHeightMm,
            Math.Round(codeMm, 1),
            Math.Round(codeMm / render.ModuleCount, 3)));
    }

    /// <summary>Padding baked into .qrPrintCard on the sheet; kept in sync with the stylesheet.</summary>
    private const double LabelPaddingMm = 1.5;
    /// <summary>A code of the real shape, so the preview shows the density a printed label will have.
    /// A shorter placeholder would quietly promise a code that scans better than the real one.</summary>
    private const string SampleScanCode = "K7M2QX9V4B";

    private const string SampleAssetName = "Dell Latitude 5450";
    private const string SampleAssetTag = "LAP-0014";
    private const string SampleSerialNumber = "5CG3210XYZ";

    private static QrLabelSettingsResponse ToQrLabelResponse(Organization organization) => new(
        organization.QrLabelShowName,
        organization.QrLabelShowTag,
        organization.QrLabelShowSerialNumber,
        organization.QrLabelShowOrganizationName,
        organization.QrLabelCustomText,
        organization.QrLabelLogo,
        organization.QrLabelCodeSize,
        organization.QrLabelFormat,
        organization.HasCustomQrLabelLogo,
        organization.Name);

    private static IReadOnlyList<AssetStatusSettingResponse> BuiltInStatusSettings(string language)
    {
        if (language != "pl")
        {
            return
            [
                new(nameof(AssetStatus.Draft), "Draft", "#475569", "#f8fafc", 10, true),
                new(nameof(AssetStatus.InStock), "In stock", "#047857", "#ecfdf5", 20, true),
                new(nameof(AssetStatus.Reserved), "Reserved", "#1d4ed8", "#eff6ff", 30, true),
                new(nameof(AssetStatus.Assigned), "Assigned", "#1d4ed8", "#eff6ff", 40, true),
                new(nameof(AssetStatus.PendingReturn), "Pending return", "#b45309", "#fffbeb", 45, true),
                new(nameof(AssetStatus.InTransit), "In transit", "#c2410c", "#fff7ed", 50, true),
                new(nameof(AssetStatus.InService), "In service", "#c2410c", "#fff7ed", 60, true),
                new(nameof(AssetStatus.Damaged), "Damaged", "#be123c", "#fff1f2", 70, true),
                new(nameof(AssetStatus.Lost), "Lost", "#be123c", "#fff1f2", 80, true),
                new(nameof(AssetStatus.Retired), "Retired", "#475569", "#f8fafc", 90, true),
                new(nameof(AssetStatus.Disposed), "Disposed", "#991b1b", "#fef2f2", 100, true)
            ];
        }

        return
        [
            new(nameof(AssetStatus.Draft), "Szkic", "#475569", "#f8fafc", 10, true),
            new(nameof(AssetStatus.InStock), "W magazynie", "#047857", "#ecfdf5", 20, true),
            new(nameof(AssetStatus.Reserved), "Zarezerwowane", "#1d4ed8", "#eff6ff", 30, true),
            new(nameof(AssetStatus.Assigned), "Wydane", "#1d4ed8", "#eff6ff", 40, true),
            new(nameof(AssetStatus.PendingReturn), "Oczekuje na zwrot", "#b45309", "#fffbeb", 45, true),
            new(nameof(AssetStatus.InTransit), "W drodze", "#c2410c", "#fff7ed", 50, true),
            new(nameof(AssetStatus.InService), "W serwisie", "#c2410c", "#fff7ed", 60, true),
            new(nameof(AssetStatus.Damaged), "Uszkodzone", "#be123c", "#fff1f2", 70, true),
            new(nameof(AssetStatus.Lost), "Zaginione", "#be123c", "#fff1f2", 80, true),
            new(nameof(AssetStatus.Retired), "Wycofane", "#475569", "#f8fafc", 90, true),
            new(nameof(AssetStatus.Disposed), "Zutylizowane", "#991b1b", "#fef2f2", 100, true)
        ];
    }
}
