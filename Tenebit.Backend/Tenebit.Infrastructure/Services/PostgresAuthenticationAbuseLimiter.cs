using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Atomic fixed-window limiter shared by all API replicas. Only a SHA-256 partition key is persisted;
/// raw email/IP are never written to the rate-limit table.
/// </summary>
public sealed class PostgresAuthenticationAbuseLimiter : IAuthenticationAbuseLimiter
{
    private readonly TenebitDbContext _db;
    private readonly IClock _clock;

    public PostgresAuthenticationAbuseLimiter(TenebitDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<bool> TryAcquireAsync(string action, string accountKey, string? clientIp, int permitLimit, TimeSpan window, CancellationToken cancellationToken)
    {
        if (permitLimit <= 0 || window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(permitLimit));
        var normalizedAccount = accountKey.Trim().ToLowerInvariant();
        var normalizedIp = clientIp?.Trim() ?? "unknown";
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{action}\n{normalizedAccount}\n{normalizedIp}")));
        var now = _clock.UtcNow;
        var windowTicks = window.Ticks;
        var bucketTicks = now.UtcTicks - (now.UtcTicks % windowTicks);
        var bucketStart = new DateTimeOffset(bucketTicks, TimeSpan.Zero);
        var expiresAt = bucketStart.Add(window).Add(window);

        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO tenebit.auth_rate_limit_buckets ("KeyHash", "BucketStart", "Count", "ExpiresAt")
                VALUES (@key, @bucket, 1, @expires)
                ON CONFLICT ("KeyHash", "BucketStart") DO UPDATE
                SET "Count" = tenebit.auth_rate_limit_buckets."Count" + 1,
                    "ExpiresAt" = EXCLUDED."ExpiresAt"
                WHERE tenebit.auth_rate_limit_buckets."Count" < @limit
                RETURNING "Count";
                """;
            command.Parameters.AddWithValue("key", keyHash);
            command.Parameters.AddWithValue("bucket", bucketStart);
            command.Parameters.AddWithValue("expires", expiresAt);
            command.Parameters.AddWithValue("limit", permitLimit);
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
