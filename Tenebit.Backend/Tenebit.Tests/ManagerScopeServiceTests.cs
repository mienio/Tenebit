using Tenebit.Application.Common;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class ManagerScopeServiceTests
{
    [Fact]
    public async Task ResolveVisiblePersonIdsAsync_ReturnsNullForOrgWideRole()
    {
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var service = new ManagerScopeService(people, teams);
        var user = new FakeCurrentUser { Roles = ["owner"] };

        var result = await service.ResolveVisiblePersonIdsAsync(user, [TenebitRoles.Owner, TenebitRoles.Admin], CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveVisiblePersonIdsAsync_ScopesPlainManagerToOwnTeam()
    {
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var service = new ManagerScopeService(people, teams);
        var user = new FakeCurrentUser { Roles = ["manager"] };

        var manager = new Person(user.OrganizationId, "Anna", "Kierownik", user.Email);
        people.Add(manager);
        var team = new Team(user.OrganizationId, "Zespół A", manager.Id, null);
        teams.Add(team);
        var teammate = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        teammate.Update(teammate.FirstName, teammate.LastName, teammate.Email, null, null, "Pracownik", null, team.Id, null, null, null);
        people.Add(teammate);
        var outsider = new Person(user.OrganizationId, "Ola", "Inna", "ola@acme.test");
        people.Add(outsider);

        var result = await service.ResolveVisiblePersonIdsAsync(user, [TenebitRoles.Owner, TenebitRoles.Admin], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(manager.Id, result);
        Assert.Contains(teammate.Id, result);
        Assert.DoesNotContain(outsider.Id, result);
    }

    [Fact]
    public async Task ResolveVisiblePersonIdsAsync_IncludesDirectReportsWithoutFormalTeam()
    {
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var service = new ManagerScopeService(people, teams);
        var user = new FakeCurrentUser { Roles = ["manager"] };

        var manager = new Person(user.OrganizationId, "Anna", "Kierownik", user.Email);
        people.Add(manager);
        var directReport = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        directReport.Update(directReport.FirstName, directReport.LastName, directReport.Email, null, null, "Pracownik", null, null, manager.Id, null, null);
        people.Add(directReport);

        var result = await service.ResolveVisiblePersonIdsAsync(user, [TenebitRoles.Owner, TenebitRoles.Admin], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(directReport.Id, result);
    }
}
