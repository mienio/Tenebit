using System.Security.Cryptography;

namespace Tenebit.Api.Auth.OAuth;

public static class PkceHelper
{
    public static string NewCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(48));

    public static string NewState() => Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

    public static string ChallengeFor(string codeVerifier) => Base64UrlEncode(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
