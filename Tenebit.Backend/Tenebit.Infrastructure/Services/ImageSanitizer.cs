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
    // Limit pikseli sprawdzany PRZED pełnym dekodowaniem (Image.Identify nie materializuje bufora pikseli) —
    // bez tego mały skompresowany plik może rozpakować się do gigabajtów w pamięci (decompression/pixel bomb,
    // audyt P0.4).
    private const int MaxDimensionPx = 8000;
    private const long MaxPixels = 40_000_000L;

    public SanitizedImage StripMetadata(DetectedImageFormat format, byte[] content)
    {
        try
        {
            var info = Image.Identify(content) ?? throw new DomainException("Nieprawidłowy lub uszkodzony plik obrazu.");
            if (info.Width > MaxDimensionPx || info.Height > MaxDimensionPx || (long)info.Width * info.Height > MaxPixels)
            {
                throw new DomainException("Obraz ma zbyt duże wymiary.");
            }

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
