using Tenebit.Application.People;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class PeopleServiceTests
{
    private static (PeopleService Service, FakeCurrentUser User, InMemoryPersonRepository People) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var service = new PeopleService(people, new InMemoryTeamRepository(), new InMemoryActivityLogRepository(), currentUser, new FakeClock(), new FakeUnitOfWork());
        return (service, currentUser, people);
    }

    private static CreatePersonRequest BuildRequest(string email) =>
        new("Jan", "Kowalski", email, null, null, PersonRelationType.Employee, null, null, null, null, null);

    [Fact]
    public async Task CreateAsync_RejectsUserWithoutHrOrAdminRole()
    {
        var (service, user, _) = CreateService();
        user.Roles = ["employee"];

        var result = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateEmailWithinOrganization()
    {
        var (service, _, _) = CreateService();

        var first = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_SucceedsForHrRole()
    {
        var (service, user, _) = CreateService();
        user.Roles = ["hr"];

        var result = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Jan Kowalski", result.Value!.FullName);
    }

    [Fact]
    public async Task DeleteAsync_RejectsWhenPersonHasBlockingRelations()
    {
        var (service, _, people) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);
        people.HasBlockingRelations = true;

        var result = await service.DeleteAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_SucceedsWhenNoBlockingRelations()
    {
        var (service, _, people) = CreateService();
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
        var (service, _, _) = CreateService();
        var created = await service.CreateAsync(BuildRequest("jan@acme.test"), CancellationToken.None);

        var updateRequest = new UpdatePersonRequest("Jan", "Kowalski", "jan@acme.test", null, null, PersonRelationType.Employee, null, null, null, null, null, false);
        var result = await service.UpdateAsync(created.Value!.Id, updateRequest, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
    }
}
