using Tenebit.Application.Common;
using Tenebit.Application.Licenses;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class LicenseServiceTests
{
    private static (LicenseService Service, FakeCurrentUser User, InMemoryLicenseRepository Licenses, InMemorySubscriptionRepository Subscriptions) CreateService()
    {
        var user = new FakeCurrentUser();
        var licenses = new InMemoryLicenseRepository();
        var people = new InMemoryPersonRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var service = new LicenseService(licenses, people, new InMemoryRolePermissionRepository(), new InMemoryActivityLogRepository(), user, new FakeClock(), new FakeUnitOfWork(), subscriptions);
        return (service, user, licenses, subscriptions);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenAtSubscriptionResourceLimit()
    {
        var (service, user, licenses, subscriptions) = CreateService();
        subscriptions.Add(new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key));

        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            licenses.Add(new License(user.OrganizationId, $"License {i}", "Vendor", null, 1, null, null));
        }

        var result = await service.CreateAsync(new CreateLicenseRequest("License over limit", "Vendor", null, 1, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Limit licencji przekroczony", result.Error!.Message);
    }
}
