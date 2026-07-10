namespace Tenebit.Application.Abstractions;

public interface IQrCodeGenerator
{
    string CreateAssetQrSvg(string payload);
    string CreateTotpQrSvg(string otpAuthUri);
}
