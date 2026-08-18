using Tenebit.Api.Auth;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class TwoFactorChallengeStoreTests
{
    [Fact]
    public async Task Challenge_IsSharedRepositoryBacked_AndSingleUse()
    {
        var repository = new InMemoryTwoFactorChallengeRepository();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var firstReplica = new TwoFactorChallengeStore(repository, new FakeUnitOfWork(), clock);
        var secondReplica = new TwoFactorChallengeStore(repository, new FakeUnitOfWork(), clock);
        var userId = Guid.NewGuid();

        var ticket = await firstReplica.CreateAsync(userId, CancellationToken.None);

        Assert.Equal(userId, await secondReplica.ConsumeAsync(ticket, CancellationToken.None));
        Assert.Null(await firstReplica.ConsumeAsync(ticket, CancellationToken.None));
    }
}
