using Tenebit.Application.Common;

namespace Tenebit.Application.Assignments;

/// <summary>
/// Rozpakowuje `data:image/png;base64,...` przysłane przez canvas podpisu.
///
/// Wejście pochodzi z anonimowego endpointu, więc nagłówek data-URL jest sprawdzany, a nie zakładany;
/// zawartość i tak przechodzi potem przez sanitizer i detekcję sygnatury pliku.
/// </summary>
public static class SignatureDataUrl
{
    private const string PngPrefix = "data:image/png;base64,";

    // 200 KB binarnie to ~273 KB w base64; limit trzyma dekodowanie z dala od dużych alokacji.
    private const int MaxEncodedLength = 300_000;

    public static Result<byte[]> Decode(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return Result<byte[]>.Failure(Error.Validation("Podpis jest pusty."));
        }

        var trimmed = dataUrl.Trim();
        if (trimmed.Length > MaxEncodedLength)
        {
            return Result<byte[]>.Failure(Error.Validation("Podpis może mieć maksymalnie 200 KB."));
        }

        if (!trimmed.StartsWith(PngPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Result<byte[]>.Failure(Error.Validation("Podpis musi być obrazem PNG."));
        }

        try
        {
            return Result<byte[]>.Success(Convert.FromBase64String(trimmed[PngPrefix.Length..]));
        }
        catch (FormatException)
        {
            return Result<byte[]>.Failure(Error.Validation("Podpis jest uszkodzony."));
        }
    }
}
