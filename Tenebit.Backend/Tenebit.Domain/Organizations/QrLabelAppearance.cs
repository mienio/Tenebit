namespace Tenebit.Domain.Organizations;

/// <summary>
/// The label's appearance without the organization it belongs to, so the settings editor can render a
/// preview of unsaved changes without the draft ever touching a tracked entity.
/// </summary>
public sealed record QrLabelAppearance(
    bool ShowName,
    bool ShowTag,
    bool ShowSerialNumber,
    bool ShowOrganizationName,
    string? CustomText,
    QrLabelLogoMode Logo,
    QrLabelCodeSize CodeSize,
    QrLabelFormat Format);
