using QRCoder;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public string CreateAssetQrSvg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new SvgQRCode(data);
        return qr.GetGraphic(4);
    }
}
