using System.Text;

namespace Tenebit.Application.Identity;

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        int bitBuffer = 0, bitCount = 0;

        foreach (var b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                bitCount -= 5;
                builder.Append(Alphabet[(bitBuffer >> bitCount) & 0x1F]);
            }
        }

        if (bitCount > 0)
        {
            builder.Append(Alphabet[(bitBuffer << (5 - bitCount)) & 0x1F]);
        }

        return builder.ToString();
    }

    public static byte[] Decode(string base32)
    {
        var cleaned = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(cleaned.Length * 5 / 8);
        int bitBuffer = 0, bitCount = 0;

        foreach (var c in cleaned)
        {
            var index = Alphabet.IndexOf(c);
            if (index < 0) continue;
            bitBuffer = (bitBuffer << 5) | index;
            bitCount += 5;
            if (bitCount >= 8)
            {
                bitCount -= 8;
                bytes.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
