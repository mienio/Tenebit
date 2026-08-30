using System.Security.Cryptography;
using System.Text;

namespace Tenebit.Domain.Assets;

/// <summary>
/// The short, unguessable identifier printed inside an asset's QR code.
///
/// Ten characters is not an arbitrary round number: it is the longest code that still fits the smallest
/// QR version able to hold a URL at all. Measured on the real payload, 8 and 10 characters both produce
/// a 33x33 code, so the two extra characters - and the 10 extra bits of entropy they carry - are free.
/// Anything longer pushes the code to 37x37 and makes every printed label denser for no benefit.
///
/// The alphabet is upper-case alphanumeric because that is what keeps the whole URL inside QR
/// alphanumeric mode (5.5 bits per character instead of 8). I, L, O and U are left out: they are the
/// characters people misread when a label is scuffed and someone falls back to typing the code, and
/// dropping them costs nothing here.
///
/// 50 bits is sized against online guessing only - there is no offline attack, because a guess has to be
/// sent to the server - and what a correct guess buys is the organization's name plus a rate-limited
/// fault report. At a thousand attempts a second, exhausting half the space takes tens of thousands of
/// years.
/// </summary>
public static class AssetScanCode
{
    public const int Length = 10;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Create()
    {
        var builder = new StringBuilder(Length);
        Span<byte> buffer = stackalloc byte[Length];
        RandomNumberGenerator.Fill(buffer);

        // The alphabet has exactly 32 symbols, so masking the low five bits of each byte is uniform -
        // no modulo bias to reason about.
        foreach (var value in buffer)
        {
            builder.Append(Alphabet[value & 31]);
        }

        return builder.ToString();
    }

    public static bool IsWellFormed(string? code) =>
        code is { Length: Length } && code.All(character => Alphabet.Contains(character));
}
