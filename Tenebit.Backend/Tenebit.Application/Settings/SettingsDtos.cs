using Tenebit.Domain.Organizations;

namespace Tenebit.Application.Settings;

public sealed record AssetStatusSettingResponse(string StatusKey, string Label, string Color, string BackgroundColor, int SortOrder, bool IsEnabled);
[ValidatedRequest]
public sealed record SaveAssetStatusSettingRequest(string StatusKey, string Label, string Color, string BackgroundColor, int SortOrder, bool IsEnabled);

public sealed record EvidencePrivacySettingsResponse(PublicIpCaptureMode CapturePublicIp, int? PublicIpRetentionDays, int? DefaultEvidenceRetentionMonths, string? PrivacyNoticeUrl, string? PrivacyContactEmail);
[ValidatedRequest]
public sealed record SaveEvidencePrivacySettingsRequest(PublicIpCaptureMode CapturePublicIp, int? PublicIpRetentionDays, int? DefaultEvidenceRetentionMonths, string? PrivacyNoticeUrl, string? PrivacyContactEmail);

public sealed record QrLabelSettingsResponse(
    bool ShowName,
    bool ShowTag,
    bool ShowSerialNumber,
    bool ShowOrganizationName,
    string? CustomText,
    QrLabelLogoMode Logo,
    QrLabelCodeSize CodeSize,
    QrLabelFormat Format,
    bool HasCustomLogo,
    string OrganizationName);

[ValidatedRequest]
public sealed record SaveQrLabelSettingsRequest(
    bool ShowName,
    bool ShowTag,
    bool ShowSerialNumber,
    bool ShowOrganizationName,
    string? CustomText,
    QrLabelLogoMode Logo,
    QrLabelCodeSize CodeSize,
    QrLabelFormat Format);

/// <summary>
/// Sample label plus the geometry the editor needs to state, in millimetres, how big the printed code
/// will actually be. Without those numbers the trade-off between caption lines and code size is
/// invisible until a sheet has been printed and a phone refuses to read it.
/// </summary>
public sealed record QrLabelPreviewResponse(
    string Svg,
    int WidthPx,
    int HeightPx,
    int CodeSizePx,
    int ModuleCount,
    double LabelWidthMm,
    double LabelHeightMm,
    double CodeMm,
    double MillimetresPerModule);
