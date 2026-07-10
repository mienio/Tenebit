using QRCoder;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public string CreateAssetQrSvg(string payload) => CreateSvg(payload);

    public string CreateTotpQrSvg(string otpAuthUri) => CreateSvg(otpAuthUri);

    private static string CreateSvg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new SvgQRCode(data);
        return qr.GetGraphic(4);
    }
}
