using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

// Tenebit jest wdrażany jako pojedyncza instancja API (audyt P0.7) — OAuth/2FA state
// (OAuthStateStore/TwoFactorChallengeStore) żyje w IMemoryCache per-proces, a background jobs
// (AlertBackgroundService i inne) nie mają distributed locka, więc druga równoległa instancja
// dublowałaby joby i gubiła cudzy OAuth/2FA state bez żadnego widocznego błędu. Ten guard trzyma
// session-scoped pg_try_advisory_lock przez cały czas życia procesu; jeśli lock jest już zajęty,
// start aplikacji kończy się głośnym błędem zamiast ciche uruchomienie drugiej instancji.
public sealed class SingleInstanceGuardService : IHostedService
{
    private const string LockKey = "tenebit-api-single-instance";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SingleInstanceGuardService> _logger;
    private IServiceScope? _scope;
    private TenebitDbContext? _db;

    public SingleInstanceGuardService(IServiceScopeFactory scopeFactory, ILogger<SingleInstanceGuardService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _scope = _scopeFactory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        await _db.Database.OpenConnectionAsync(cancellationToken);

        var acquired = await _db.Database
            .SqlQuery<bool>($"SELECT pg_try_advisory_lock(hashtext({LockKey}))")
            .SingleAsync(cancellationToken);

        if (!acquired)
        {
            throw new InvalidOperationException(
                "Inna instancja Tenebit API już trzyma single-instance advisory lock w tej bazie danych. " +
                "Deployment jest skonfigurowany jako single-instance (audyt P0.7) — zatrzymaj poprzedni proces przed startem nowego.");
        }

        _logger.LogInformation("Single-instance advisory lock zajęty przez ten proces.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_db is not null)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock(hashtext({LockKey}))", cancellationToken);
            await _db.Database.CloseConnectionAsync();
        }

        _scope?.Dispose();
    }
}
