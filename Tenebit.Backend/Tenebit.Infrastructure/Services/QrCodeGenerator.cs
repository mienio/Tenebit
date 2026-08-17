using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using QRCoder;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    private const int PixelsPerModule = 4;
    private const int LabelLineHeight = 22;
    private const int LabelPadding = 6;

    public string CreateAssetQrSvg(string payload) => CreateSvg(payload);

    public string CreateLabelledAssetQrSvg(string payload, IReadOnlyList<string> labelLines)
    {
        var lines = labelLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrSvg = new SvgQRCode(data).GetGraphic(PixelsPerModule);
        if (lines.Count == 0) return qrSvg;

        var qrSize = data.ModuleMatrix.Count * PixelsPerModule;
        var labelHeight = lines.Count * LabelLineHeight + LabelPadding * 2;
        var totalHeight = qrSize + labelHeight;
        var positionedQr = Regex.Replace(qrSvg, "^<svg ", $"<svg x=\"0\" y=\"{labelHeight}\" ");

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{qrSize}\" height=\"{totalHeight}\" viewBox=\"0 0 {qrSize} {totalHeight}\">");
        sb.Append($"<rect width=\"{qrSize}\" height=\"{totalHeight}\" fill=\"#ffffff\"/>");
        for (var i = 0; i < lines.Count; i++)
        {
            var y = LabelPadding + (i + 1) * LabelLineHeight - 6;
            sb.Append($"<text x=\"{qrSize / 2}\" y=\"{y}\" text-anchor=\"middle\" font-family=\"Arial, sans-serif\" font-size=\"16\" font-weight=\"600\" fill=\"#111111\">{WebUtility.HtmlEncode(lines[i])}</text>");
        }
        sb.Append(positionedQr);
        sb.Append("</svg>");
        return sb.ToString();
    }

    public string CreateTotpQrSvg(string otpAuthUri) => CreateSvg(otpAuthUri);

    private static string CreateSvg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new SvgQRCode(data);
        return qr.GetGraphic(PixelsPerModule);
    }
}
