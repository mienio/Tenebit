using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Admin;
using Tenebit.Application.Common;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Closes every affiliate payout period whose calendar month has ended (spec §7.1 step 1) - runs daily
/// rather than "on the 1st" specifically, so a missed run (deploy, restart) still catches up instead of
/// waiting a full month. Closing a period only freezes its total and flips it to AwaitingPayout; the
/// admin still has to actually pay it and confirm that by hand (see AffiliateAdminService.MarkPayoutPaidAsync).
/// </summary>
public sealed class AffiliatePayoutPeriodCloseBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AffiliatePayoutPeriodCloseBackgroundService> _logger;

    public AffiliatePayoutPeriodCloseBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<AffiliatePayoutPeriodCloseBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = Math.Max(1, _configuration.GetValue("AffiliatePayoutPeriods:IntervalHours", 24));
        var interval = TimeSpan.FromHours(intervalHours);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var gate = scope.ServiceProvider.GetRequiredService<PostgresJobLock>();
                var admin = scope.ServiceProvider.GetRequiredService<AffiliateAdminService>();
                await gate.TryRunAsync(
                    "affiliate-payout-period-close",
                    interval,
                    async ct =>
                    {
                        var closed = await admin.CloseDuePeriodsAsync(ct);
                        if (closed > 0)
                        {
                            _logger.LogInformation("Closed {ClosedCount} affiliate payout periods.", closed);
                        }
                    },
                    stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SecurityTelemetry.BackgroundJobFailure();
                _logger.LogError(ex, "Affiliate payout period close job failed; it will retry on the next cycle.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
