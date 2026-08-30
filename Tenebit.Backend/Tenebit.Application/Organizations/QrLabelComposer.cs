using Tenebit.Application.Abstractions;
using Tenebit.Domain.Organizations;

namespace Tenebit.Application.Organizations;

/// <summary>
/// Turns the organization's label settings into the concrete lines printed around one asset's QR code.
/// Both the asset endpoint and the settings preview go through here, so what an admin sees while editing
/// is produced by the same code that prints the sheet.
/// </summary>
public static class QrLabelComposer
{
    public static QrLabelContent Compose(Organization organization, string assetName, string assetTag, string? serialNumber) =>
        Compose(organization.QrLabelAppearance, organization.Name, organization.QrLabelLogoImage, organization.QrLabelLogoContentType, assetName, assetTag, serialNumber);

    public static QrLabelContent Compose(
        QrLabelAppearance appearance,
        string organizationName,
        byte[]? customLogo,
        string? customLogoContentType,
        string assetName,
        string assetTag,
        string? serialNumber)
    {
        var header = new List<string>();
        if (appearance.ShowOrganizationName) header.Add(organizationName);
        if (!string.IsNullOrWhiteSpace(appearance.CustomText)) header.Add(appearance.CustomText!);

        // The tag leads the footer because it is the one line someone reads off a shelf without scanning.
        var footer = new List<string>();
        if (appearance.ShowTag) footer.Add(assetTag);
        if (appearance.ShowName) footer.Add(assetName);
        if (appearance.ShowSerialNumber && !string.IsNullOrWhiteSpace(serialNumber)) footer.Add(serialNumber!);

        var logo = appearance.Logo switch
        {
            QrLabelLogoMode.Tenebit => new QrLabelLogoImage(null, null, true),
            QrLabelLogoMode.Custom when customLogo is { Length: > 0 } => new QrLabelLogoImage(customLogo, customLogoContentType, false),
            _ => null
        };

        return new QrLabelContent(header, footer, logo, PixelsPerModule(appearance.CodeSize));
    }

    public static int PixelsPerModule(QrLabelCodeSize size) => size switch
    {
        QrLabelCodeSize.Small => 3,
        QrLabelCodeSize.Large => 6,
        _ => 4
    };

    /// <summary>Label stock in millimetres, matching the formats offered on the print sheet.</summary>
    public static (double Width, double Height) FormatMillimetres(QrLabelFormat format) => format switch
    {
        QrLabelFormat.Square38 => (38, 38),
        QrLabelFormat.Large99 => (99.1, 67.7),
        _ => (63.5, 38.1)
    };
}
