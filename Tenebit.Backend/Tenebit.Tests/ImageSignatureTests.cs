using Tenebit.Application.Common;

namespace Tenebit.Tests;

public class ImageSignatureTests
{
    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/webp", true)]
    [InlineData("application/pdf", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsAllowedContentType_ValidatesAllowlist(string? contentType, bool expected)
    {
        Assert.Equal(expected, ImageSignature.IsAllowedContentType(contentType));
    }

    [Fact]
    public void Detect_RecognizesJpegMagicBytes()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 };
        Assert.Equal(DetectedImageFormat.Jpeg, ImageSignature.Detect(bytes));
    }

    [Fact]
    public void Detect_RecognizesPngMagicBytes()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        Assert.Equal(DetectedImageFormat.Png, ImageSignature.Detect(bytes));
    }

    [Fact]
    public void Detect_RecognizesWebpMagicBytes()
    {
        var bytes = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' };
        Assert.Equal(DetectedImageFormat.Webp, ImageSignature.Detect(bytes));
    }

    [Fact]
    public void Detect_ReturnsUnknownForGarbageBytes()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Assert.Equal(DetectedImageFormat.Unknown, ImageSignature.Detect(bytes));
    }
}
