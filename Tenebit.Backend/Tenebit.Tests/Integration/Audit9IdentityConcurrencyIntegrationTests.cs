using Microsoft.Extensions.DependencyInjection;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Identity;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class Audit9IdentityConcurrencyIntegrationTests : IClassFixture<TenebitApiFactory>
{
    private readonly TenebitApiFactory _factory;
    public Audit9IdentityConcurrencyIntegrationTests(TenebitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PasswordResetToken_ConcurrentConsume_ExactlyOneWins()
    {
        var (_, user, _) = await _factory.SeedTenantAsync("ResetRace", "owner");
        var raw = $"reset-{Guid.NewGuid():N}";
        var hash = TokenHasher.Hash(raw);
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<TenebitDbContext>();
            db.PasswordResetTokens.Add(new PasswordResetToken(user.Id, hash, DateTimeOffset.UtcNow.AddHours(1)));
            await db.SaveChangesAsync();
        }

        async Task<Guid?> Consume()
        {
            using var scope = _factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPasswordResetTokenRepository>();
            return await repo.TryConsumeAsync(hash, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var results = await Task.WhenAll(Consume(), Consume());
        Assert.Single(results.Where(x => x == user.Id));
        Assert.Single(results.Where(x => x is null));
    }

    [Fact]
    public async Task RecoveryCode_ConcurrentConsume_ExactlyOneWins()
    {
        var (_, user, _) = await _factory.SeedTenantAsync("RecoveryRace", "owner");
        var hash = TokenHasher.Hash($"recovery-{Guid.NewGuid():N}");
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<TenebitDbContext>();
            db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode(user.Id, hash, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        async Task<bool> Consume()
        {
            using var scope = _factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ITwoFactorRecoveryCodeRepository>();
            return await repo.TryConsumeAsync(user.Id, hash, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var results = await Task.WhenAll(Consume(), Consume());
        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(1, results.Count(x => !x));
    }

    [Fact]
    public async Task DistributedAuthLimiter_DoesNotTreatFiftyUsersBehindOneNatAsOneAccount()
    {
        const string natIp = "203.0.113.25";

        // Kubełek limitera to SHA256(action + konto + IP) w oknie 5 minut, zapisany w bazie. Przy stałych
        // nazwach drugi przebieg testu w ciągu tych 5 minut trafiał w kubełek zapełniony przez poprzedni
        // i przyjmował 0 zamiast 10. Unikalny action izoluje przebiegi, a wspólne natIp w obrębie jednego
        // przebiegu nadal odwzorowuje 50 użytkowników za jednym NAT-em.
        var run = Guid.NewGuid().ToString("N");

        for (var i = 0; i < 50; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var limiter = scope.ServiceProvider.GetRequiredService<IAuthenticationAbuseLimiter>();
            Assert.True(await limiter.TryAcquireAsync($"login-test-{run}", $"user{i}@example.test", natIp, 10, TimeSpan.FromMinutes(5), CancellationToken.None));
        }

        var accepted = 0;
        for (var i = 0; i < 11; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var limiter = scope.ServiceProvider.GetRequiredService<IAuthenticationAbuseLimiter>();
            if (await limiter.TryAcquireAsync($"brute-test-{run}", "victim@example.test", natIp, 10, TimeSpan.FromMinutes(5), CancellationToken.None)) accepted++;
        }
        Assert.Equal(10, accepted);
    }
}
