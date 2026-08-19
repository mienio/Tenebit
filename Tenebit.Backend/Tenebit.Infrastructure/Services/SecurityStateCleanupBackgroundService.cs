using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Common;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

public sealed class SecurityStateCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SecurityStateCleanupBackgroundService> _logger;

    public SecurityStateCleanupBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SecurityStateCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var jobLock = scope.ServiceProvider.GetRequiredService<PostgresJobLock>();
                var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                await jobLock.TryRunAsync("security-state-cleanup", Interval, async ct =>
                {
                    var cutoff = clock.UtcNow.AddHours(-1);
                    await db.OAuthTransactions.Where(x => x.ExpiresAt < cutoff).ExecuteDeleteAsync(ct);
                    await db.TwoFactorChallenges.Where(x => x.ExpiresAt < cutoff).ExecuteDeleteAsync(ct);
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM tenebit.auth_rate_limit_buckets WHERE \"ExpiresAt\" < {clock.UtcNow}", ct);
                }, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SecurityTelemetry.BackgroundJobFailure();
                _logger.LogError(ex, "Security-state cleanup failed; it will retry on the next cycle.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
