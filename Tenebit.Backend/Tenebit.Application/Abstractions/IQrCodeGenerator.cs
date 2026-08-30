namespace Tenebit.Application.Abstractions;

/// <summary>
/// What is printed around the QR code on an asset label.
///
/// Header lines sit above the code (the organization's mark and name - what identifies the owner),
/// footer lines below it (what identifies the individual unit). The split is deliberate: on a small
/// label the reader scans downwards from the mark to the tag, and mixing the two groups makes a strip
/// of identical-looking text.
///
/// <paramref name="PixelsPerModule"/> is what decides how much of the finished label the code claims.
/// The label is scaled to fit the stock it is printed on, so a bigger module means a bigger code and
/// proportionally smaller text - the two compete for the same paper.
/// </summary>
public sealed record QrLabelContent(
    IReadOnlyList<string> HeaderLines,
    IReadOnlyList<string> FooterLines,
    QrLabelLogoImage? Logo,
    int PixelsPerModule = 4)
{
    public static QrLabelContent Empty { get; } = new([], [], null);
}

/// <summary>Raw bytes of the mark to embed, or <see cref="UseTenebitMark"/> for the built-in one.</summary>
public sealed record QrLabelLogoImage(byte[]? Content, string? ContentType, bool UseTenebitMark);

/// <summary>
/// The rendered label plus the numbers needed to answer "will this actually scan": how tall the whole
/// label is against how much of it is code, and how many modules that code is divided into. The editor
/// turns those into millimetres for the chosen stock, so the trade-off between caption lines and code
/// size is visible before a sheet is printed rather than after.
/// </summary>
public sealed record QrLabelRender(string Svg, int WidthPx, int HeightPx, int CodeSizePx, int ModuleCount);

public interface IQrCodeGenerator
{
    string CreateAssetQrSvg(string payload);
    string CreateLabelledAssetQrSvg(string payload, QrLabelContent content);
    QrLabelRender RenderAssetQrLabel(string payload, QrLabelContent content);
    string CreateTotpQrSvg(string otpAuthUri);
}
