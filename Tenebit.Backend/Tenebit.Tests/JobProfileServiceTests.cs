using Tenebit.Domain.Subscriptions;
using Tenebit.Domain.JobProfiles;
using Tenebit.Application.JobProfiles;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class JobProfileServiceTests
{
    private static (JobProfileService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryJobProfileRepository Profiles) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var profiles = new InMemoryJobProfileRepository();
        var service = new JobProfileService(
            profiles,
            new InMemoryAssetCategoryRepository(),
            new InMemoryProcedureRepository(),
            people,
            new InMemorySubscriptionRepository(),
            new InMemoryActivityLogRepository(),
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork());
        return (service, currentUser, people, profiles);
    }

    [Fact]
    public async Task CreateAsync_RejectsCrossOrganizationDefaultManagerId()
    {
        var (service, _, people, _) = CreateService();
        var otherOrgManager = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        people.Add(otherOrgManager);

        var request = new SaveJobProfileRequest("Programista", null, otherOrgManager.Id, [], []);
        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_SucceedsWithDefaultManagerFromSameOrganization()
    {
        var (service, user, people, _) = CreateService();
        var manager = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(manager);

        var request = new SaveJobProfileRequest("Programista", null, manager.Id, [], []);
        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenAtSubscriptionResourceLimit()
    {
        var (service, user, _, profiles) = CreateService();
        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            profiles.Add(new JobProfile(user.OrganizationId, $"Zestaw {i}", null, null));
        }

        var result = await service.CreateAsync(new SaveJobProfileRequest("Zestaw ponad limit", null, null, [], []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("SUBSCRIPTION_RESOURCE_LIMIT_EXCEEDED", result.Error!.Code);
    }
}
