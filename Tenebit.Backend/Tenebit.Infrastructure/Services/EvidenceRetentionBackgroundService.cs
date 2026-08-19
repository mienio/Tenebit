using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Common;
using Tenebit.Application.Evidence;

namespace Tenebit.Infrastructure.Services;

public sealed class EvidenceRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EvidenceRetentionBackgroundService> _logger;

    public EvidenceRetentionBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<EvidenceRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("EvidenceRetention:Enabled", true))
        {
            _logger.LogInformation("Zadanie retencji materiału dowodowego wyłączone (EvidenceRetention:Enabled=false).");
            return;
        }

        var intervalMinutes = _configuration.GetValue("EvidenceRetention:IntervalMinutes", 1440);
        var interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var gate = scope.ServiceProvider.GetRequiredService<PostgresJobLock>();
                var retentionService = scope.ServiceProvider.GetRequiredService<EvidenceRetentionService>();
                await gate.TryRunAsync("evidence-retention", interval, retentionService.RunAsync, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SecurityTelemetry.BackgroundJobFailure();
                _logger.LogError(ex, "Zadanie retencji materiału dowodowego zakończyło się błędem - spróbuję ponownie przy kolejnym cyklu.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
