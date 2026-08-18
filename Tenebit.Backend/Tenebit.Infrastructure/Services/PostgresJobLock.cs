using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Durable PostgreSQL execution gate for periodic jobs. The claim row is updated in the same transaction
/// as the job, so concurrent replicas serialize and a second replica that arrives after the winner has
/// already completed still skips the same interval. If the job fails, the transaction rolls back and a
/// different replica may retry.
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

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;
        var eligibleBefore = now.Subtract(minimumInterval);

        var claimed = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO tenebit.background_job_runs ("JobName", "LastRunAt")
            VALUES ({jobName}, {now})
            ON CONFLICT ("JobName") DO UPDATE
            SET "LastRunAt" = EXCLUDED."LastRunAt"
            WHERE tenebit.background_job_runs."LastRunAt" <= {eligibleBefore};
            """, cancellationToken);

        if (claimed != 1) return false;

        await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
