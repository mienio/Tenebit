using Tenebit.Domain.Subscriptions;
using Tenebit.Application.People;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class TeamServiceTests
{
    private static (TeamService Service, FakeCurrentUser User, InMemoryTeamRepository Teams, InMemoryPersonRepository People) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var teams = new InMemoryTeamRepository();
        var people = new InMemoryPersonRepository();
        var service = new TeamService(teams, people, new InMemorySubscriptionRepository(), new InMemoryActivityLogRepository(), currentUser, new FakeClock(), new FakeUnitOfWork());
        return (service, currentUser, teams, people);
    }

    [Fact]
    public async Task CreateAsync_RejectsCrossOrganizationManagerId()
    {
        var (service, _, _, people) = CreateService();
        var otherOrgManager = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        people.Add(otherOrgManager);

        var result = await service.CreateAsync(new CreateTeamRequest("Zespół IT", otherOrgManager.Id, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_SucceedsWithManagerFromSameOrganization()
    {
        var (service, user, _, people) = CreateService();
        var manager = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(manager);

        var result = await service.CreateAsync(new CreateTeamRequest("Zespół IT", manager.Id, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAsync_RejectsCrossOrganizationManagerId()
    {
        var (service, user, teams, people) = CreateService();
        var team = new Team(user.OrganizationId, "Zespół IT", null, null);
        teams.Add(team);
        var otherOrgManager = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        people.Add(otherOrgManager);

        var result = await service.UpdateAsync(team.Id, new UpdateTeamRequest("Zespół IT", otherOrgManager.Id, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenAtSubscriptionResourceLimit()
    {
        var (service, user, teams, _) = CreateService();
        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            teams.Add(new Team(user.OrganizationId, $"Zespol {i}", null, null));
        }

        var result = await service.CreateAsync(new CreateTeamRequest("Zespol ponad limit", null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("SUBSCRIPTION_RESOURCE_LIMIT_EXCEEDED", result.Error!.Code);
    }
}
