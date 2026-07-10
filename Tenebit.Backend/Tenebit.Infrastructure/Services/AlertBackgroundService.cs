using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Alerts;

namespace Tenebit.Infrastructure.Services;

public sealed class AlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertBackgroundService> _logger;

    public AlertBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<AlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Alerts:Enabled", true))
        {
            _logger.LogInformation("Sprawdzanie alertów wyłączone (Alerts:Enabled=false).");
            return;
        }

        var intervalHours = _configuration.GetValue("Alerts:IntervalHours", 24);
        var interval = TimeSpan.FromHours(Math.Max(1, intervalHours));

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var alertCheckService = scope.ServiceProvider.GetRequiredService<AlertCheckService>();
                await alertCheckService.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Sprawdzanie alertów zakończyło się błędem — spróbuję ponownie przy kolejnym cyklu.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
