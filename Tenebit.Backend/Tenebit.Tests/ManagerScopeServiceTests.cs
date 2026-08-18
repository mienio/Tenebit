using Tenebit.Application.Common;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class ManagerScopeServiceTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsNullForOrgWideRole()
    {
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var service = new ManagerScopeService(people, teams);
        var user = new FakeCurrentUser { Roles = ["owner"] };

        var result = await service.ResolveAsync(user, [TenebitRoles.Owner, TenebitRoles.Admin], CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_UnlinkedManagerFailsClosed()
    {
        var service = new ManagerScopeService(new InMemoryPersonRepository(), new InMemoryTeamRepository());
        var user = new FakeCurrentUser { Roles = ["manager"], PersonId = null };

        var result = await service.ResolveAsync(user, [TenebitRoles.Owner, TenebitRoles.Admin], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.PersonIds);
        Assert.Empty(result.TeamIds);
    }

    [Fact]
    public async Task ResolveAsync_ScopesPlainManagerToOwnTeam()
    {
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var service = new ManagerScopeService(people, teams);
        var user = new FakeCurrentUser { Roles = ["manager"] };

        var manager = new Person(user.OrganizationId, "Anna", "Kierownik", user.Email);
        people.Add(manager);
        user.PersonId = manager.Id;
        var team = new Team(user.OrganizationId, "Zespół A", manager.Id, null);
        teams.Add(team);
        var teammate = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        teammate.Update(teammate.FirstName, teammate.LastName, teammate.Email, null, null, "Pracownik", null, team.Id, null, null, null);
        people.Add(teammate);
        var outsider = new Person(user.OrganizationId, "Ola", "Inna", "ola@acme.test");
        people.Add(outsider);

        var result = await service.ResolveAsync(user, [TenebitRoles.Owner, TenebitRoles.Admin], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(manager.Id, result!.PersonIds);
        Assert.Contains(teammate.Id, result.PersonIds);
        Assert.Contains(team.Id, result.TeamIds);
        Assert.DoesNotContain(outsider.Id, result.PersonIds);
    }

    [Fact]
    public async Task ResolveAsync_IncludesDirectReportsWithoutFormalTeam()
    {
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var service = new ManagerScopeService(people, teams);
        var user = new FakeCurrentUser { Roles = ["manager"] };

        var manager = new Person(user.OrganizationId, "Anna", "Kierownik", user.Email);
        people.Add(manager);
        user.PersonId = manager.Id;
        var directReport = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        directReport.Update(directReport.FirstName, directReport.LastName, directReport.Email, null, null, "Pracownik", null, null, manager.Id, null, null);
        people.Add(directReport);

        var result = await service.ResolveAsync(user, [TenebitRoles.Owner, TenebitRoles.Admin], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(directReport.Id, result!.PersonIds);
    }
}
