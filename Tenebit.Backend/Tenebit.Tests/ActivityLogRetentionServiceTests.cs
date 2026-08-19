using Tenebit.Application.Audit;
using Tenebit.Domain.Audit;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class ActivityLogRetentionServiceTests
{
    [Fact]
    public async Task RunAsync_DeletesOnlyRowsOlderThanConfiguredRetention()
    {
        var now = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock { UtcNow = now };
        var repository = new InMemoryActivityLogRepository();
        var organizationId = Guid.NewGuid();
        repository.Add(new ActivityLog(organizationId, "old", "asset", Guid.NewGuid(), "system", null, now.AddMonths(-25)));
        repository.Add(new ActivityLog(organizationId, "keep", "asset", Guid.NewGuid(), "system", null, now.AddMonths(-23)));
        var service = new ActivityLogRetentionService(repository, clock);

        var deleted = await service.RunAsync(24, 100, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Single(repository.Logs);
        Assert.Equal("keep", repository.Logs[0].Action);
    }
}
