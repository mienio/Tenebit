namespace Tenebit.Domain.Common;

/// <summary>
/// Proof-of-receipt seals hash the moment of signing, so the hashed text has to be something the database
/// can give back unchanged. It could not be: DateTimeOffset carries 100-nanosecond ticks while PostgreSQL's
/// "timestamp with time zone" keeps microseconds, so sealing the raw clock value produced a hash over a
/// timestamp that was truncated on the way to disk. Every later verification recomputed the hash from the
/// truncated value, got a different digest and reported the protocol as tampered with, even though nothing
/// had been touched (QA BUG-001: "INTEGRITY COMPROMISED" on a perfectly valid admin confirmation).
/// </summary>
public static class IntegritySealTimestamp
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    /// <summary>Drops the sub-microsecond ticks the database cannot store, so the sealed value survives a round trip.</summary>
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TicksPerMicrosecond));
    }

    /// <summary>The exact text a seal hashes for <paramref name="value"/>.</summary>
    public static string Text(DateTimeOffset value) => Normalize(value).ToString("O");

    /// <summary>
    /// The texts a seal written before normalization could have hashed. The sub-microsecond digit was
    /// dropped by the database and cannot be recovered, so verification of a legacy seal has to try all ten
    /// values it could have held. This does not weaken the seal: every candidate agrees on all the business
    /// facts and differs only in a digit that was never persisted, so altering an asset, a person, a
    /// procedure or a photo still has to defeat SHA-256.
    /// </summary>
    public static IEnumerable<string> LegacyTexts(DateTimeOffset value)
    {
        var normalized = Normalize(value);
        for (var tick = 0; tick < TicksPerMicrosecond; tick++)
        {
            yield return normalized.AddTicks(tick).ToString("O");
        }
    }
}
