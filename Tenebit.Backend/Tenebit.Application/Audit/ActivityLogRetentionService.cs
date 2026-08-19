using Tenebit.Application.Abstractions;

namespace Tenebit.Application.Audit;

/// <summary>Removes old audit rows in bounded batches so activity_logs cannot grow without limit.</summary>
public sealed class ActivityLogRetentionService
{
    private readonly IActivityLogRepository _activity;
    private readonly IClock _clock;

    public ActivityLogRetentionService(IActivityLogRepository activity, IClock clock)
    {
        _activity = activity;
        _clock = clock;
    }

    public async Task<int> RunAsync(int retentionMonths, int batchSize, CancellationToken cancellationToken)
    {
        if (retentionMonths <= 0) throw new ArgumentOutOfRangeException(nameof(retentionMonths));
        var safeBatchSize = Math.Clamp(batchSize, 100, 5_000);
        var cutoff = _clock.UtcNow.AddMonths(-retentionMonths);
        var totalDeleted = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var deleted = await _activity.DeleteOlderThanAsync(cutoff, safeBatchSize, cancellationToken);
            totalDeleted += deleted;
            if (deleted < safeBatchSize) break;
        }

        return totalDeleted;
    }
}
