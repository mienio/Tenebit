namespace Tenebit.Application.Common;

public enum DetectedImageFormat { Unknown, Jpeg, Png, Webp }

public static class ImageSignature
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public static bool IsAllowedContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.Contains(contentType.Trim().ToLowerInvariant());

    public static DetectedImageFormat Detect(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return DetectedImageFormat.Jpeg;
        if (content.Length >= 8 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47
            && content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A)
            return DetectedImageFormat.Png;
        if (content.Length >= 12 && content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
            && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P')
            return DetectedImageFormat.Webp;
        return DetectedImageFormat.Unknown;
    }
}
