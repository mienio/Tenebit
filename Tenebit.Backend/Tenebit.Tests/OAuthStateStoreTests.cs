using Tenebit.Api.Auth.OAuth;
using Tenebit.Application.Identity;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class OAuthStateStoreTests
{
    private static OAuthStateStore Create(InMemoryOAuthTransactionRepository repo, FakeClock clock) => new(repo, new FakeUnitOfWork(), clock);

    [Fact]
    public async Task CallbackWithoutMatchingCorrelation_DoesNotConsumeState()
    {
        var repo = new InMemoryOAuthTransactionRepository(); var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var first = Create(repo, clock); var second = Create(repo, clock);
        const string correlation = "browser-correlation";
        var state = await first.CreateAsync("google", "verifier", "/dashboard", TokenHasher.Hash(correlation), "nonce", CancellationToken.None);
        Assert.Null(await second.TryConsumeAsync(state, "google", "different-browser", CancellationToken.None));
        Assert.NotNull(await second.TryConsumeAsync(state, "google", correlation, CancellationToken.None));
    }

    [Fact]
    public async Task CallbackWithDifferentProvider_DoesNotConsumeState()
    {
        var repo = new InMemoryOAuthTransactionRepository(); var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var store = Create(repo, clock); const string correlation = "browser-correlation";
        var state = await store.CreateAsync("google", "verifier", "/dashboard", TokenHasher.Hash(correlation), "nonce", CancellationToken.None);
        Assert.Null(await store.TryConsumeAsync(state, "microsoft", correlation, CancellationToken.None));
        Assert.NotNull(await store.TryConsumeAsync(state, "google", correlation, CancellationToken.None));
    }

    [Fact]
    public async Task ValidState_IsSharedAcrossReplicas_AndSingleUse()
    {
        var repo = new InMemoryOAuthTransactionRepository(); var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var first = Create(repo, clock); var second = Create(repo, clock); const string correlation = "browser-correlation";
        var state = await first.CreateAsync("google", "verifier", "/dashboard", TokenHasher.Hash(correlation), "nonce", CancellationToken.None);
        Assert.NotNull(await second.TryConsumeAsync(state, "google", correlation, CancellationToken.None));
        Assert.Null(await first.TryConsumeAsync(state, "google", correlation, CancellationToken.None));
    }
}
