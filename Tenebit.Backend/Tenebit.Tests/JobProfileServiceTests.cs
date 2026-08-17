using Tenebit.Application.JobProfiles;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class JobProfileServiceTests
{
    private static (JobProfileService Service, FakeCurrentUser User, InMemoryPersonRepository People) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var service = new JobProfileService(
            new InMemoryJobProfileRepository(),
            new InMemoryAssetCategoryRepository(),
            new InMemoryProcedureRepository(),
            people,
            new InMemoryActivityLogRepository(),
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork());
        return (service, currentUser, people);
    }

    [Fact]
    public async Task CreateAsync_RejectsCrossOrganizationDefaultManagerId()
    {
        var (service, _, people) = CreateService();
        var otherOrgManager = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        people.Add(otherOrgManager);

        var request = new SaveJobProfileRequest("Programista", null, otherOrgManager.Id, [], []);
        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_SucceedsWithDefaultManagerFromSameOrganization()
    {
        var (service, user, people) = CreateService();
        var manager = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(manager);

        var request = new SaveJobProfileRequest("Programista", null, manager.Id, [], []);
        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
