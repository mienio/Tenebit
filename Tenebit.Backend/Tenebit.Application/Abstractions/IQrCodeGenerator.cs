namespace Tenebit.Application.Abstractions;

public interface IQrCodeGenerator
{
    string CreateAssetQrSvg(string payload);
    string CreateLabelledAssetQrSvg(string payload, IReadOnlyList<string> labelLines);
    string CreateTotpQrSvg(string otpAuthUri);
}
