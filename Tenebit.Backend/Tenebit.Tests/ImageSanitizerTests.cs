using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Tenebit.Application.Common;
using Tenebit.Domain.Common;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class ImageSanitizerTests
{
    private static byte[] CreateJpegWithGpsExif()
    {
        using var image = new Image<Rgba32>(4, 4);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitude, new Rational[] { new(52, 1), new(13, 1), new(0, 1) });
        exif.SetValue(ExifTag.GPSLongitude, new Rational[] { new(21, 1), new(0, 1), new(0, 1) });
        exif.SetValue(ExifTag.Make, "TestCam");
        image.Metadata.ExifProfile = exif;

        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder());
        return stream.ToArray();
    }

    [Fact]
    public void StripMetadata_RemovesExifGpsData()
    {
        var original = CreateJpegWithGpsExif();
        var sanitizer = new ImageSanitizer();

        var sanitized = sanitizer.StripMetadata(DetectedImageFormat.Jpeg, original);

        using var result = Image.Load(sanitized.Content);
        Assert.True(result.Metadata.ExifProfile is null || !result.Metadata.ExifProfile.Values.Any());
    }

    [Fact]
    public void StripMetadata_ProducesValidShaThatDiffersFromOriginal()
    {
        var original = CreateJpegWithGpsExif();
        var sanitizer = new ImageSanitizer();

        var sanitized = sanitizer.StripMetadata(DetectedImageFormat.Jpeg, original);

        Assert.Matches("^[0-9a-f]{64}$", sanitized.Sha256);
        var originalSha = Convert.ToHexStringLower(SHA256.HashData(original));
        Assert.NotEqual(originalSha, sanitized.Sha256);
    }

    [Fact]
    public void StripMetadata_ThrowsDomainExceptionForNonImageBytes()
    {
        var bogus = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 not an image");
        var sanitizer = new ImageSanitizer();

        Assert.Throws<DomainException>(() => sanitizer.StripMetadata(DetectedImageFormat.Jpeg, bogus));
    }
}
