using Tenebit.Application.Common;
using Tenebit.Application.People;
using Tenebit.Domain.Assets;
using Tenebit.Domain.People;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class PeopleServiceTests
{
    private static (PeopleService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryAssetRepository Assets, InMemoryActivityLogRepository Activity, InMemorySubscriptionRepository Subscriptions) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var assets = new InMemoryAssetRepository();
        var activity = new InMemoryActivityLogRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var service = new PeopleService(people, teams, activity, currentUser, new FakeClock(), new FakeUnitOfWork(), new ManagerScopeService(people, teams), new Tenebit.Application.Assets.LocationReferenceResolver(new InMemoryLocationRepository()), subscriptions);
        return (service, currentUser, people, assets, activity, subscriptions);
    }

    private static (PeopleService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryTeamRepository Teams) CreateServiceWithTeams()
    {
        var currentUser = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var service = new PeopleService(people, teams, new InMemoryActivityLogRepository(), currentUser, new FakeClock(), new FakeUnitOfWork(), new ManagerScopeService(people, teams), new Tenebit.Application.Assets.LocationReferenceResolver(new InMemoryLocationRepository()), new InMemorySubscriptionRepository());
        return (service, currentUser, people, teams);
    }

    private static CreatePersonRequest BuildRequest(string email) =>
        new("Jan", "Kowalski", email, null, null, "Pracownik", null, null, null, null, null);

    [Fact]
    public async Task CreateAsync_RejectsUserWithoutHrOrAdminRole()
    {
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = ["employee"];

        var result = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateEmailWithinOrganization()
    {
        var (service, _, _, _, _, _) = CreateService();

        var first = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_SucceedsForHrRole()
    {
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = ["hr"];

        var result = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Jan Kowalski", result.Value!.FullName);
    }

    [Fact]
    public async Task ListAsync_ManagerOnlySeesOwnTeamMembers()
    {
        var (service, user, people, teams) = CreateServiceWithTeams();
        var manager = new Person(user.OrganizationId, "Anna", "Kierownik", user.Email);
        people.Add(manager);
        var team = new Team(user.OrganizationId, "Zespół A", manager.Id, null);
        teams.Add(team);
        var teammate = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        teammate.Update(teammate.FirstName, teammate.LastName, teammate.Email, null, null, "Pracownik", null, team.Id, null, null, null);
        people.Add(teammate);
        var outsider = new Person(user.OrganizationId, "Ola", "Inna", "ola@acme.test");
        people.Add(outsider);

        user.Roles = ["manager"];
        user.PersonId = manager.Id;

        var result = await service.ListAsync(null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var ids = result.Value!.Select(p => p.Id).ToHashSet();
        Assert.Contains(manager.Id, ids);
        Assert.Contains(teammate.Id, ids);
        Assert.DoesNotContain(outsider.Id, ids);
    }

    [Fact]
    public async Task GetAsync_ManagerCannotReadPersonOutsideManagedTeam()
    {
        var (service, user, people, teams) = CreateServiceWithTeams();
        var manager = new Person(user.OrganizationId, "Anna", "Kierownik", user.Email);
        people.Add(manager);
        var outsider = new Person(user.OrganizationId, "Ola", "Inna", "ola@acme.test");
        people.Add(outsider);

        user.Roles = ["manager"];
        user.PersonId = manager.Id;

        var result = await service.GetAsync(outsider.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_RejectsWhenPersonHasBlockingRelations()
    {
        var (service, _, people, _, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        people.HasBlockingRelations = true;

        var result = await service.DeleteAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_SucceedsWhenNoBlockingRelations()
    {
        var (service, _, people, _, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        people.HasBlockingRelations = false;

        var result = await service.DeleteAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var afterDelete = await service.GetAsync(created.Value!.Id, CancellationToken.None);
        Assert.True(afterDelete.IsFailure);
    }

    [Fact]
    public async Task UpdateAsync_CanDeactivatePerson()
    {
        var (service, _, _, _, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        var updateRequest = new UpdatePersonRequest("Jan", "Kowalski", "jan@acme.test", null, null, "Pracownik", null, null, null, null, null, false);
        var result = await service.UpdateAsync(created.Value!.Id, updateRequest, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
        Assert.Equal(EmploymentStatus.Inactive, result.Value.EmploymentStatus);
        Assert.NotNull(result.Value.DeactivatedAt);
    }

    [Fact]
    public async Task UpdateAsync_PreservesOffboardingWhenLegacyClientSendsIsActiveTrue()
    {
        var (service, _, people, _, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        var person = people.People.Single();
        person.StartOffboarding(DateTimeOffset.UtcNow.AddDays(7));

        var updateRequest = new UpdatePersonRequest("Jan", "Kowalski", "jan@acme.test", null, null, "Pracownik", "Developer", null, null, null, null, true, "en");
        var result = await service.UpdateAsync(created.Value!.Id, updateRequest, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsActive);
        Assert.Equal(EmploymentStatus.Offboarding, result.Value.EmploymentStatus);
        Assert.Equal("en", result.Value.PreferredLanguage);
    }

    [Fact]
    public void Person_LifecycleMaintainsCompatibilityInvariants()
    {
        var person = new Person(Guid.NewGuid(), "Jan", "Kowalski", "jan@acme.test");
        var employmentEndsAt = new DateTimeOffset(2026, 8, 20, 16, 0, 0, TimeSpan.FromHours(2));

        Assert.Equal(EmploymentStatus.Active, person.EmploymentStatus);
        Assert.True(person.IsActive);
        Assert.True(person.CanReceiveNewObligations);

        person.StartOffboarding(employmentEndsAt);

        Assert.Equal(EmploymentStatus.Offboarding, person.EmploymentStatus);
        Assert.True(person.IsActive);
        Assert.False(person.CanReceiveNewObligations);
        Assert.Equal(employmentEndsAt.ToUniversalTime(), person.EmploymentEndsAt);

        var deactivatedAt = employmentEndsAt.AddDays(1);
        person.Deactivate(deactivatedAt);

        Assert.Equal(EmploymentStatus.Inactive, person.EmploymentStatus);
        Assert.False(person.IsActive);
        Assert.Equal(deactivatedAt.ToUniversalTime(), person.DeactivatedAt);

        person.Deactivate(deactivatedAt.AddHours(1));
        Assert.Equal(deactivatedAt.ToUniversalTime(), person.DeactivatedAt);

        person.Activate();
        Assert.Equal(EmploymentStatus.Active, person.EmploymentStatus);
        Assert.True(person.IsActive);
        Assert.Null(person.EmploymentEndsAt);
        Assert.Null(person.DeactivatedAt);
    }

    [Fact]
    public void Person_CannotStartOffboardingTwice()
    {
        var person = new Person(Guid.NewGuid(), "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Throws<Tenebit.Domain.Common.DomainException>(() => person.StartOffboarding(DateTimeOffset.UtcNow.AddDays(8)));
    }







    [Fact]
    public async Task CreateAsync_RejectsCrossOrganizationTeamId()
    {
        var (service, _, _, teams) = CreateServiceWithTeams();
        var otherOrgTeam = new Team(Guid.NewGuid(), "Inny zespół", null, null);
        teams.Add(otherOrgTeam);

        var request = new CreatePersonRequest("Jan", "Kowalski", "jan@acme.test", null, null, "Pracownik", null, otherOrgTeam.Id, null, null, null);
        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsCrossOrganizationManagerId()
    {
        var (service, _, people, _) = CreateServiceWithTeams();
        var otherOrgManager = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        people.Add(otherOrgManager);

        var request = new CreatePersonRequest("Jan", "Kowalski", "jan@acme.test", null, null, "Pracownik", null, null, otherOrgManager.Id, null, null);
        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }


    [Fact]
    public async Task CreateAsync_RejectsWhenAtSubscriptionResourceLimit()
    {
        var (service, user, people, _, _, subscriptions) = CreateService();
        subscriptions.Add(new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key));

        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            people.Add(new Person(user.OrganizationId, "Jan", $"Kowalski{i}", $"jan{i}@acme.test"));
        }

        var result = await service.CreateAsync(BuildRequest("jan-over-limit@acme.test"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Limit pracowników przekroczony", result.Error!.Message);
    }
}
