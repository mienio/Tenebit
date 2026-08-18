using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Compatibility stub retained so overlay patches do not require deleting an old source file.
/// OAuth/2FA state and periodic-job coordination are now PostgreSQL-backed, so the API is intentionally
/// allowed to run with multiple replicas. Do not reintroduce a process-wide singleton lock here.
/// </summary>
[Obsolete("HA is supported; this compatibility service must not be registered.")]
public sealed class SingleInstanceGuardService : IHostedService
{
    private readonly ILogger<SingleInstanceGuardService> _logger;
    public SingleInstanceGuardService(ILogger<SingleInstanceGuardService> logger) => _logger = logger;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Obsolete SingleInstanceGuardService was registered. It no longer acquires a global lock.");
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
