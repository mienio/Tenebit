using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Durable multi-replica gate for periodic jobs. The claim is one short PostgreSQL statement and is
/// committed before the job executes, so SMTP/Stripe/other network I/O never holds an open DB transaction.
/// On an observed failure the claim is released conditionally, allowing another replica to retry.
/// </summary>
public sealed class PostgresJobLock
{
    private readonly TenebitDbContext _db;
    private readonly IClock _clock;

    public PostgresJobLock(TenebitDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<bool> TryRunAsync(string jobName, TimeSpan minimumInterval, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobName)) throw new ArgumentException("Job name is required.", nameof(jobName));
        if (minimumInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumInterval));

        var claimedAt = _clock.UtcNow;
        var eligibleBefore = claimedAt.Subtract(minimumInterval);
        var claimed = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO tenebit.background_job_runs ("JobName", "LastRunAt")
            VALUES ({jobName}, {claimedAt})
            ON CONFLICT ("JobName") DO UPDATE
            SET "LastRunAt" = EXCLUDED."LastRunAt"
            WHERE tenebit.background_job_runs."LastRunAt" <= {eligibleBefore};
            """, cancellationToken);

        if (claimed != 1) return false;

        try
        {
            await action(cancellationToken);
            return true;
        }
        catch
        {
            // Best-effort release only if this worker still owns the exact claim. If the process is
            // hard-killed, minimumInterval acts as the bounded lease and prevents a permanent lock.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE tenebit.background_job_runs
                SET "LastRunAt" = {eligibleBefore.Subtract(TimeSpan.FromSeconds(1))}
                WHERE "JobName" = {jobName} AND "LastRunAt" = {claimedAt};
                """, CancellationToken.None);
            throw;
        }
    }
}
