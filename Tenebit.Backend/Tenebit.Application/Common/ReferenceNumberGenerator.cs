namespace Tenebit.Application.Common;

public static class ReferenceNumberGenerator
{
    public static string Create(string prefix, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return $"{prefix.Trim().ToUpperInvariant()}-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
