using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Audit;
using Tenebit.Application.Common;

namespace Tenebit.Infrastructure.Services;

public sealed class ActivityLogRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ActivityLogRetentionBackgroundService> _logger;

    public ActivityLogRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ActivityLogRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("ActivityLogRetention:Enabled", true))
        {
            _logger.LogInformation("Activity log retention disabled (ActivityLogRetention:Enabled=false).");
            return;
        }

        var intervalMinutes = Math.Max(60, _configuration.GetValue("ActivityLogRetention:IntervalMinutes", 1440));
        var retentionMonths = Math.Max(1, _configuration.GetValue("ActivityLogRetention:Months", 24));
        var batchSize = Math.Clamp(_configuration.GetValue("ActivityLogRetention:BatchSize", 1000), 100, 5_000);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var gate = scope.ServiceProvider.GetRequiredService<PostgresJobLock>();
                var retention = scope.ServiceProvider.GetRequiredService<ActivityLogRetentionService>();
                await gate.TryRunAsync(
                    "activity-log-retention",
                    interval,
                    async ct =>
                    {
                        var deleted = await retention.RunAsync(retentionMonths, batchSize, ct);
                        if (deleted > 0)
                        {
                            _logger.LogInformation("Activity log retention deleted {DeletedCount} rows older than {RetentionMonths} months.", deleted, retentionMonths);
                        }
                    },
                    stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SecurityTelemetry.BackgroundJobFailure();
                _logger.LogError(ex, "Activity log retention failed; it will retry on the next cycle.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
