using Tenebit.Application.Audit;
using Tenebit.Domain.Identity;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class ActivityLogServiceTests
{
    private static (ActivityLogService Service, Guid OrganizationId) CreateService(params string[] userDisplayNames)
    {
        var currentUser = new FakeCurrentUser();
        var users = new InMemoryOrganizationUserRepository();
        foreach (var name in userDisplayNames)
        {
            var user = new OrganizationUser(currentUser.OrganizationId, $"{name.ToLowerInvariant()}@acme.test", name, true);
            users.Add(user);
        }

        var service = new ActivityLogService(new InMemoryActivityLogRepository(), users, currentUser);
        return (service, currentUser.OrganizationId);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyImmediately_WhenActorFilterMatchesNoUser()
    {
        var (service, _) = CreateService("Anna Kowalska", "Piotr Nowak");

        var result = await service.ListAsync(1, 25, null, null, null, null, null, "nieistniejacy-uzytkownik", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value!.Total);
    }

    [Fact]
    public async Task ListAsync_ProceedsToQuery_WhenActorFilterMatchesAKnownUser()
    {
        var (service, _) = CreateService("Anna Kowalska");

        var result = await service.ListAsync(1, 25, null, null, null, null, null, "Kowalska", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ListAsync_DoesNotFilterByActor_WhenNoActorProvided()
    {
        var (service, _) = CreateService("Anna Kowalska");

        var result = await service.ListAsync(1, 25, null, null, null, null, null, null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
