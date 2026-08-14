using Tenebit.Application.Common;

namespace Tenebit.Application.Abstractions;

public sealed record SanitizedImage(byte[] Content, string ContentType, long SizeBytes, string Sha256);

public interface IImageSanitizer
{
    SanitizedImage StripMetadata(DetectedImageFormat format, byte[] content);
}
