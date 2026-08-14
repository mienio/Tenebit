using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Common;

namespace Tenebit.Infrastructure.Services;

public sealed class ImageSanitizer : IImageSanitizer
{
    public SanitizedImage StripMetadata(DetectedImageFormat format, byte[] content)
    {
        try
        {
            using var image = Image.Load(content);

            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;
            foreach (var frame in image.Frames)
            {
                frame.Metadata.ExifProfile = null;
                frame.Metadata.IccProfile = null;
                frame.Metadata.IptcProfile = null;
                frame.Metadata.XmpProfile = null;
            }

            using var output = new MemoryStream();
            string contentType;
            switch (format)
            {
                case DetectedImageFormat.Png:
                    image.Save(output, new PngEncoder());
                    contentType = "image/png";
                    break;
                case DetectedImageFormat.Webp:
                    image.Save(output, new WebpEncoder());
                    contentType = "image/webp";
                    break;
                default:
                    image.Save(output, new JpegEncoder());
                    contentType = "image/jpeg";
                    break;
            }

            var bytes = output.ToArray();
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            return new SanitizedImage(bytes, contentType, bytes.LongLength, sha256);
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            throw new DomainException("Nieprawidłowy lub uszkodzony plik obrazu.");
        }
    }
}
