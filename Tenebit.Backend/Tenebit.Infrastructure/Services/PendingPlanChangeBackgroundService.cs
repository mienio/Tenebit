using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Common;
using Tenebit.Application.Subscriptions;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Lands scheduled downgrades on Paddle on their effective date. Separate from
/// <see cref="SubscriptionReconciliationBackgroundService"/> because it is time-critical: the switch has to
/// be in place before Paddle raises the renewal invoice, whereas ordinary reconciliation is happy with a
/// six-hourly sweep. PostgresJobLock keeps replicas from doing it twice.
/// </summary>
public sealed class PendingPlanChangeBackgroundService : BackgroundService
{
    // Comfortably inside SubscriptionReconciliationService.PlanChangeLead, so a change is never missed
    // between two passes even if one of them fails outright.
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingPlanChangeBackgroundService> _logger;

    public PendingPlanChangeBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PendingPlanChangeBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var gate = scope.ServiceProvider.GetRequiredService<PostgresJobLock>();
                var service = scope.ServiceProvider.GetRequiredService<SubscriptionReconciliationService>();
                await gate.TryRunAsync("paddle-pending-plan-changes", Interval, service.ApplyDuePlanChangesAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SecurityTelemetry.BackgroundJobFailure();
                _logger.LogError(ex, "Scheduled Paddle plan-change cycle failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
