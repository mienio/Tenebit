using Tenebit.Application.Common;
using Tenebit.Application.Licenses;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.People;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class LicenseServiceTests
{
    private static (LicenseService Service, FakeCurrentUser User, InMemoryLicenseRepository Licenses, InMemorySubscriptionRepository Subscriptions, InMemoryPersonRepository People) CreateService()
    {
        var user = new FakeCurrentUser();
        var licenses = new InMemoryLicenseRepository();
        var people = new InMemoryPersonRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var service = new LicenseService(licenses, people, new InMemoryRolePermissionRepository(), TestAuthorization.Permissions(user), new InMemoryActivityLogRepository(), user, new FakeClock(), new FakeUnitOfWork(), subscriptions);
        return (service, user, licenses, subscriptions, people);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenAtSubscriptionResourceLimit()
    {
        var (service, user, licenses, subscriptions, _) = CreateService();
        subscriptions.Add(new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key));

        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            licenses.Add(new License(user.OrganizationId, $"License {i}", "Vendor", null, 1, null, null));
        }

        var result = await service.CreateAsync(new CreateLicenseRequest("License over limit", "Vendor", null, 1, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Limit licencji przekroczony", result.Error!.Message);
    }

    [Fact]
    public async Task AssignSeatAsync_AktywnaOsoba_ZajmujeStanowisko()
    {
        var (service, user, licenses, _, people) = CreateService();
        var license = new License(user.OrganizationId, "Figma", "Figma Inc.", null, 3, null, null);
        licenses.Add(license);
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(person);

        var result = await service.AssignSeatAsync(license.Id, new AssignLicenseSeatRequest(person.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.SeatsAssigned);
    }

    [Fact]
    public async Task AssignSeatAsync_NieaktywnaOsoba_JestOdrzucana()
    {
        // QA: BUG-002 - osoba nieaktywna jest wykluczona z wydań i offboardingu, więc stanowisko
        // licencji też jest dla niej niedostępne. Spójność, nie tylko ukrycie w interfejsie.
        var (service, user, licenses, _, people) = CreateService();
        var license = new License(user.OrganizationId, "Figma", "Figma Inc.", null, 3, null, null);
        licenses.Add(license);
        var person = new Person(user.OrganizationId, "Anna", "Nowak", "anna@acme.test");
        person.Deactivate(DateTimeOffset.UtcNow);
        people.Add(person);

        var result = await service.AssignSeatAsync(license.Id, new AssignLicenseSeatRequest(person.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(licenses.Licenses.Single().Seats);
    }
}
