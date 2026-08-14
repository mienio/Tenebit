using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.People;

namespace Tenebit.Infrastructure.Services;

public sealed class OffboardingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OffboardingBackgroundService> _logger;

    public OffboardingBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<OffboardingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Offboarding:Enabled", true))
        {
            _logger.LogInformation("Zadanie offboardingu wyłączone (Offboarding:Enabled=false).");
            return;
        }

        var intervalMinutes = _configuration.GetValue("Offboarding:IntervalMinutes", 60);
        var interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var schedulerService = scope.ServiceProvider.GetRequiredService<PersonOffboardingSchedulerService>();
                await schedulerService.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Zadanie offboardingu zakończyło się błędem — spróbuję ponownie przy kolejnym cyklu.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
