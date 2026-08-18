using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Common;
using Tenebit.Application.Subscriptions;

namespace Tenebit.Infrastructure.Services;

/// <summary>Shared-state-safe periodic Stripe reconciliation. PostgresJobLock prevents duplicate work across replicas.</summary>
public sealed class SubscriptionReconciliationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionReconciliationBackgroundService> _logger;

    public SubscriptionReconciliationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionReconciliationBackgroundService> logger)
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
                await gate.TryRunAsync("stripe-subscription-reconciliation", Interval, service.RunAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SecurityTelemetry.BackgroundJobFailure();
                _logger.LogError(ex, "Stripe subscription reconciliation cycle failed.");
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
