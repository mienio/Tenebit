using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Dashboard;

namespace Tenebit.Infrastructure.Services;

public sealed class DashboardSnapshotBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DashboardSnapshotBackgroundService> _logger;

    public DashboardSnapshotBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DashboardSnapshotBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("DashboardSnapshots:Enabled", true))
        {
            _logger.LogInformation("Migawki dashboardu wyłączone (DashboardSnapshots:Enabled=false).");
            return;
        }

        var intervalHours = _configuration.GetValue("DashboardSnapshots:IntervalHours", 6);
        var interval = TimeSpan.FromHours(Math.Max(1, intervalHours));

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var snapshotService = scope.ServiceProvider.GetRequiredService<DashboardSnapshotService>();
                await snapshotService.CaptureAllOrganizationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Zapis migawki dashboardu zakończył się błędem — spróbuję ponownie przy kolejnym cyklu.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
