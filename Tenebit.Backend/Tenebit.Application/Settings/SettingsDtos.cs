using Tenebit.Domain.Organizations;

namespace Tenebit.Application.Settings;

public sealed record AssetStatusSettingResponse(string StatusKey, string Label, string Color, string BackgroundColor, int SortOrder, bool IsEnabled);
public sealed record SaveAssetStatusSettingRequest(string StatusKey, string Label, string Color, string BackgroundColor, int SortOrder, bool IsEnabled);

public sealed record EvidencePrivacySettingsResponse(PublicIpCaptureMode CapturePublicIp, int? PublicIpRetentionDays, int? DefaultEvidenceRetentionMonths, string? PrivacyNoticeUrl, string? PrivacyContactEmail);
public sealed record SaveEvidencePrivacySettingsRequest(PublicIpCaptureMode CapturePublicIp, int? PublicIpRetentionDays, int? DefaultEvidenceRetentionMonths, string? PrivacyNoticeUrl, string? PrivacyContactEmail);

public sealed record QrLabelSettingsResponse(bool ShowName, bool ShowTag);
public sealed record SaveQrLabelSettingsRequest(bool ShowName, bool ShowTag);
