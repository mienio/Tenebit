using Tenebit.Application.Common;
using Tenebit.Application.People;
using Tenebit.Domain.Assets;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class PeopleServiceTests
{
    private static (PeopleService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryAssetRepository Assets, InMemoryActivityLogRepository Activity) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var assets = new InMemoryAssetRepository();
        var activity = new InMemoryActivityLogRepository();
        var service = new PeopleService(people, teams, assets, activity, currentUser, new FakeClock(), new FakeUnitOfWork(), new ManagerScopeService(people, teams));
        return (service, currentUser, people, assets, activity);
    }

    private static (PeopleService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryTeamRepository Teams) CreateServiceWithTeams()
    {
        var currentUser = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var service = new PeopleService(people, teams, new InMemoryAssetRepository(), new InMemoryActivityLogRepository(), currentUser, new FakeClock(), new FakeUnitOfWork(), new ManagerScopeService(people, teams));
        return (service, currentUser, people, teams);
    }

    private static CreatePersonRequest BuildRequest(string email) =>
        new("Jan", "Kowalski", email, null, null, "Pracownik", null, null, null, null, null);

    [Fact]
    public async Task CreateAsync_RejectsUserWithoutHrOrAdminRole()
    {
        var (service, user, _, _, _) = CreateService();
        user.Roles = ["employee"];

        var result = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateEmailWithinOrganization()
    {
        var (service, _, _, _, _) = CreateService();

        var first = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_SucceedsForHrRole()
    {
        var (service, user, _, _, _) = CreateService();
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

        var result = await service.GetAsync(outsider.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_RejectsWhenPersonHasBlockingRelations()
    {
        var (service, _, people, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        people.HasBlockingRelations = true;

        var result = await service.DeleteAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_SucceedsWhenNoBlockingRelations()
    {
        var (service, _, people, _, _) = CreateService();
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
        var (service, _, _, _, _) = CreateService();
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
        var (service, _, people, _, _) = CreateService();
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
    public async Task StartOffboardingAsync_RejectsUserWithoutOwnerAdminHrRole()
    {
        var (service, user, _, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        user.Roles = ["employee"];

        var result = await service.StartOffboardingAsync(created.Value!.Id, new StartOffboardingRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task StartOffboardingAsync_SetsOffboardingStatusAndEmploymentEndsAt()
    {
        var (service, _, _, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        var endsAt = DateTimeOffset.UtcNow.AddDays(14);

        var result = await service.StartOffboardingAsync(created.Value!.Id, new StartOffboardingRequest(endsAt), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmploymentStatus.Offboarding, result.Value!.EmploymentStatus);
        Assert.Equal(endsAt.ToUniversalTime(), result.Value.EmploymentEndsAt);
    }

    [Fact]
    public async Task StartOffboardingAsync_MovesAssignedAssetsToPendingReturn_KeepsAssignedPersonId()
    {
        var (service, _, people, assets, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        var person = people.People.Single();
        var asset = new Asset(person.OrganizationId, Guid.NewGuid(), "Laptop", "AT-1");
        asset.AssignTo(person.Id);
        assets.Add(asset);

        var result = await service.StartOffboardingAsync(person.Id, new StartOffboardingRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.PendingReturn, asset.Status);
        Assert.Equal(person.Id, asset.AssignedPersonId);
    }

    [Fact]
    public async Task StartOffboardingAsync_DoesNotTouchUnassignedOrOtherPeoplesAssets()
    {
        var (service, _, people, assets, _) = CreateService();
        var created1 = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        var created2 = await service.CreateAsync(BuildRequest("anna@acme.test"), CancellationToken.None);
        var person1 = people.People.Single(p => p.Id == created1.Value!.Id);
        var person2 = people.People.Single(p => p.Id == created2.Value!.Id);
        var unassigned = new Asset(person1.OrganizationId, Guid.NewGuid(), "Monitor", "AT-2");
        assets.Add(unassigned);
        var assignedToOther = new Asset(person1.OrganizationId, Guid.NewGuid(), "Laptop", "AT-3");
        assignedToOther.AssignTo(person2.Id);
        assets.Add(assignedToOther);

        var result = await service.StartOffboardingAsync(person1.Id, new StartOffboardingRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.InStock, unassigned.Status);
        Assert.Equal(AssetStatus.Assigned, assignedToOther.Status);
        Assert.Equal(person2.Id, assignedToOther.AssignedPersonId);
    }

    [Fact]
    public async Task StartOffboardingAsync_RejectsWhenPersonNotActive()
    {
        var (service, _, people, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        var person = people.People.Single();
        person.Deactivate(DateTimeOffset.UtcNow);

        var result = await service.StartOffboardingAsync(person.Id, new StartOffboardingRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task StartOffboardingAsync_WritesActivityLogEntry()
    {
        var (service, _, _, _, activity) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        await service.StartOffboardingAsync(created.Value!.Id, new StartOffboardingRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);

        Assert.Contains(activity.Logs, l => l.Action == "person.offboarding_started" && l.EntityId == created.Value!.Id);
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
    public async Task StartOffboardingAsync_RejectsCrossOrganizationPersonId()
    {
        var (service, _, people, _, _) = CreateService();
        var otherOrgPerson = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        people.Add(otherOrgPerson);

        var result = await service.StartOffboardingAsync(otherOrgPerson.Id, new StartOffboardingRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
