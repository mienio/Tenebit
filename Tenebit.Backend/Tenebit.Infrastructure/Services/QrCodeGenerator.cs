using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using QRCoder;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    private const int DefaultPixelsPerModule = 4;
    private const int LabelPadding = 8;
    private const int LogoHeight = 34;
    private const int HeaderLineHeight = 18;
    private const int FooterLineHeight = 22;
    private const int MinimumWidth = 180;

    public string CreateAssetQrSvg(string payload) => CreateSvg(payload);

    public string CreateLabelledAssetQrSvg(string payload, QrLabelContent content) => RenderAssetQrLabel(payload, content).Svg;

    /// <summary>
    /// Composes the label as a single self-contained SVG: the code, the surrounding text, and the mark
    /// inlined as a data URI. Self-contained matters because the browser rasterises this same string
    /// through an &lt;img&gt; and a canvas to produce the PNG/JPG downloads, and an image loaded that way
    /// is not allowed to fetch anything external - a referenced logo would come out blank.
    /// </summary>
    public QrLabelRender RenderAssetQrLabel(string payload, QrLabelContent content)
    {
        var pixelsPerModule = Math.Clamp(content.PixelsPerModule, 2, 12);
        var headerLines = content.HeaderLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        var footerLines = content.FooterLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        var logo = BuildLogoMarkup(content.Logo);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrSvg = new SvgQRCode(data).GetGraphic(pixelsPerModule);
        var moduleCount = data.ModuleMatrix.Count;
        var qrSize = moduleCount * pixelsPerModule;

        if (headerLines.Count == 0 && footerLines.Count == 0 && logo is null)
        {
            return new QrLabelRender(qrSvg, qrSize, qrSize, qrSize, moduleCount);
        }

        var width = Math.Max(qrSize, MinimumWidth);
        var logoHeight = logo is null ? 0 : LogoHeight + LabelPadding;
        var headerHeight = headerLines.Count * HeaderLineHeight;
        var footerHeight = footerLines.Count * FooterLineHeight;
        var topBlock = LabelPadding + logoHeight + headerHeight;
        if (topBlock > LabelPadding) topBlock += LabelPadding;
        var totalHeight = topBlock + qrSize + (footerLines.Count > 0 ? LabelPadding + footerHeight : 0) + LabelPadding;

        var qrX = (width - qrSize) / 2;
        var positionedQr = Regex.Replace(qrSvg, "^<svg ", $"<svg x=\"{qrX}\" y=\"{topBlock}\" ");

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{width}\" height=\"{totalHeight}\" viewBox=\"0 0 {width} {totalHeight}\">");
        sb.Append($"<rect width=\"{width}\" height=\"{totalHeight}\" fill=\"#ffffff\"/>");

        if (logo is not null)
        {
            sb.Append(logo.Replace("{{x}}", ((width - LogoHeight * 2) / 2).ToString())
                          .Replace("{{y}}", LabelPadding.ToString())
                          .Replace("{{w}}", (LogoHeight * 2).ToString())
                          .Replace("{{h}}", LogoHeight.ToString()));
        }

        for (var i = 0; i < headerLines.Count; i++)
        {
            var y = LabelPadding + logoHeight + (i + 1) * HeaderLineHeight - 5;
            sb.Append(Text(headerLines[i], width / 2, y, 13, i == 0 ? "600" : "400", "#4b4139"));
        }

        sb.Append(positionedQr);

        for (var i = 0; i < footerLines.Count; i++)
        {
            var y = topBlock + qrSize + LabelPadding + (i + 1) * FooterLineHeight - 6;
            sb.Append(Text(footerLines[i], width / 2, y, i == 0 ? 16 : 13, i == 0 ? "700" : "500", "#111111"));
        }

        sb.Append("</svg>");
        return new QrLabelRender(sb.ToString(), width, totalHeight, qrSize, moduleCount);
    }

    public string CreateTotpQrSvg(string otpAuthUri) => CreateSvg(otpAuthUri);

    private static string Text(string value, int x, int y, int fontSize, string weight, string fill) =>
        $"<text x=\"{x}\" y=\"{y}\" text-anchor=\"middle\" font-family=\"Arial, sans-serif\" font-size=\"{fontSize}\" font-weight=\"{weight}\" fill=\"{fill}\">{WebUtility.HtmlEncode(value)}</text>";

    private static string? BuildLogoMarkup(QrLabelLogoImage? logo)
    {
        if (logo is null) return null;

        if (logo.UseTenebitMark)
        {
            // The product mark, drawn as paths so it stays crisp at any print size. Colours are literal
            // because a standalone SVG has no stylesheet to resolve the brand custom properties against.
            return "<svg x=\"{{x}}\" y=\"{{y}}\" width=\"{{w}}\" height=\"{{h}}\" viewBox=\"0 0 128 128\">"
                 + "<path d=\"M64 30 L92 46 L64 62 L36 46 Z\" fill=\"#a63a2e\"/>"
                 + "<path d=\"M36 46 L64 62 L64 96 L36 80 Z\" fill=\"#221d18\"/>"
                 + "<path d=\"M92 46 L64 62 L64 96 L92 80 Z\" fill=\"#a89681\"/>"
                 + "</svg>";
        }

        if (logo.Content is not { Length: > 0 } || string.IsNullOrWhiteSpace(logo.ContentType)) return null;

        var dataUri = $"data:{logo.ContentType};base64,{Convert.ToBase64String(logo.Content)}";
        return $"<image x=\"{{{{x}}}}\" y=\"{{{{y}}}}\" width=\"{{{{w}}}}\" height=\"{{{{h}}}}\" preserveAspectRatio=\"xMidYMid meet\" xlink:href=\"{WebUtility.HtmlEncode(dataUri)}\"/>";
    }

    private static string CreateSvg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new SvgQRCode(data);
        return qr.GetGraphic(DefaultPixelsPerModule);
    }
}
