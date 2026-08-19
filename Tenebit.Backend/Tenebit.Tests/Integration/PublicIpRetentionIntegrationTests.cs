using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenebit.Domain.Audit;
using Tenebit.Infrastructure.Data;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PublicIpRetentionIntegrationTests : IClassFixture<TenebitApiFactory>
{
    private readonly TenebitApiFactory _factory;

    public PublicIpRetentionIntegrationTests(TenebitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RetentionCycle_RedactsExpiredStructuredIp_ButKeepsUnexpiredIp()
    {
        var (organization, _, _) = await _factory.SeedTenantAsync("IpRetention", "owner");
        var now = DateTimeOffset.UtcNow;
        var expired = new ActivityLog(organization.Id, "test.expired", "test", null, "public-scan", null, now.AddDays(-2), "203.0.113.0", now.AddMinutes(-1));
        var future = new ActivityLog(organization.Id, "test.future", "test", null, "public-scan", null, now, "198.51.100.0", now.AddDays(1));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            db.ActivityLogs.AddRange(expired, future);
            await db.SaveChangesAsync();
            await PublicIpRetentionBackgroundService.RunAsync(db, now, CancellationToken.None);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            var expiredRow = await db.ActivityLogs.AsNoTracking().SingleAsync(x => x.Id == expired.Id);
            var futureRow = await db.ActivityLogs.AsNoTracking().SingleAsync(x => x.Id == future.Id);

            Assert.Null(expiredRow.SourceIp);
            Assert.Null(expiredRow.SourceIpExpiresAt);
            Assert.Equal("198.51.100.0", futureRow.SourceIp);
            Assert.Equal(future.SourceIpExpiresAt, futureRow.SourceIpExpiresAt);
        }
    }
}
