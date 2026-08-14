using Tenebit.Application.People;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class PersonOffboardingSchedulerServiceTests
{
    private static (PersonOffboardingSchedulerService Service, FakeClock Clock, InMemoryOrganizationRepository Organizations, InMemoryPersonRepository People, InMemoryLicenseRepository Licenses, InMemoryActivityLogRepository Activity) CreateService()
    {
        var organizations = new InMemoryOrganizationRepository();
        var people = new InMemoryPersonRepository();
        var licenses = new InMemoryLicenseRepository();
        var activity = new InMemoryActivityLogRepository();
        var clock = new FakeClock();
        var service = new PersonOffboardingSchedulerService(organizations, people, licenses, activity, clock, new FakeUnitOfWork());
        return (service, clock, organizations, people, licenses, activity);
    }

    private static Organization CreateOrganization() => new("Acme", "PL", "pl", "PLN", "Europe/Warsaw");

    [Fact]
    public async Task RunAsync_DeactivatesPersonWhosEmploymentEndsAtHasPassed()
    {
        var (service, clock, organizations, people, _, _) = CreateService();
        var org = CreateOrganization();
        organizations.Add(org);
        var person = new Person(org.Id, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(clock.UtcNow.AddDays(-1));
        people.Add(person);

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(EmploymentStatus.Inactive, person.EmploymentStatus);
    }

    [Fact]
    public async Task RunAsync_DoesNotDeactivatePersonWhoseEmploymentEndsAtIsInFuture()
    {
        var (service, clock, organizations, people, _, _) = CreateService();
        var org = CreateOrganization();
        organizations.Add(org);
        var person = new Person(org.Id, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(clock.UtcNow.AddDays(1));
        people.Add(person);

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(EmploymentStatus.Offboarding, person.EmploymentStatus);
    }

    [Fact]
    public async Task RunAsync_DoesNotTouchActiveOrAlreadyInactivePeople()
    {
        var (service, clock, organizations, people, _, _) = CreateService();
        var org = CreateOrganization();
        organizations.Add(org);
        var activePerson = new Person(org.Id, "Jan", "Kowalski", "jan@acme.test");
        people.Add(activePerson);
        var inactivePerson = new Person(org.Id, "Anna", "Nowak", "anna@acme.test");
        inactivePerson.Deactivate(clock.UtcNow.AddDays(-10));
        people.Add(inactivePerson);
        var originalDeactivatedAt = inactivePerson.DeactivatedAt;

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(EmploymentStatus.Active, activePerson.EmploymentStatus);
        Assert.Equal(EmploymentStatus.Inactive, inactivePerson.EmploymentStatus);
        Assert.Equal(originalDeactivatedAt, inactivePerson.DeactivatedAt);
    }

    [Fact]
    public async Task RunAsync_ReleasesLicenseSeatsForDeactivatedPerson()
    {
        var (service, clock, organizations, people, licenses, _) = CreateService();
        var org = CreateOrganization();
        organizations.Add(org);
        var person = new Person(org.Id, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(clock.UtcNow.AddDays(-1));
        people.Add(person);
        var license = new License(org.Id, "Suite", null, null, 5, null, null);
        license.AssignSeat(person.Id, clock.UtcNow);
        licenses.Add(license);

        await service.RunAsync(CancellationToken.None);

        Assert.Empty(license.Seats);
    }

    [Fact]
    public async Task RunAsync_LeavesOtherPeoplesLicenseSeatsUntouched()
    {
        var (service, clock, organizations, people, licenses, _) = CreateService();
        var org = CreateOrganization();
        organizations.Add(org);
        var duePerson = new Person(org.Id, "Jan", "Kowalski", "jan@acme.test");
        duePerson.StartOffboarding(clock.UtcNow.AddDays(-1));
        people.Add(duePerson);
        var activePerson = new Person(org.Id, "Anna", "Nowak", "anna@acme.test");
        people.Add(activePerson);
        var license = new License(org.Id, "Suite", null, null, 5, null, null);
        license.AssignSeat(duePerson.Id, clock.UtcNow);
        license.AssignSeat(activePerson.Id, clock.UtcNow);
        licenses.Add(license);

        await service.RunAsync(CancellationToken.None);

        Assert.DoesNotContain(license.Seats, s => s.PersonId == duePerson.Id);
        Assert.Contains(license.Seats, s => s.PersonId == activePerson.Id);
    }

    [Fact]
    public async Task RunAsync_WritesActivityLogEntriesForDeactivationAndSeatRelease()
    {
        var (service, clock, organizations, people, licenses, activity) = CreateService();
        var org = CreateOrganization();
        organizations.Add(org);
        var person = new Person(org.Id, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(clock.UtcNow.AddDays(-1));
        people.Add(person);
        var license = new License(org.Id, "Suite", null, null, 5, null, null);
        license.AssignSeat(person.Id, clock.UtcNow);
        licenses.Add(license);

        await service.RunAsync(CancellationToken.None);

        Assert.Contains(activity.Logs, l => l.Action == "person.deactivated" && l.EntityId == person.Id && l.ActorSubject == "system");
        Assert.Contains(activity.Logs, l => l.Action == "license.seat_unassigned" && l.EntityId == license.Id && l.ActorSubject == "system");
    }

    [Fact]
    public async Task RunAsync_IsIdempotentAcrossTwoRuns()
    {
        var (service, clock, organizations, people, licenses, activity) = CreateService();
        var org = CreateOrganization();
        organizations.Add(org);
        var person = new Person(org.Id, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(clock.UtcNow.AddDays(-1));
        people.Add(person);
        var license = new License(org.Id, "Suite", null, null, 5, null, null);
        license.AssignSeat(person.Id, clock.UtcNow);
        licenses.Add(license);

        await service.RunAsync(CancellationToken.None);
        var logCountAfterFirstRun = activity.Logs.Count;
        await service.RunAsync(CancellationToken.None);

        Assert.Equal(EmploymentStatus.Inactive, person.EmploymentStatus);
        Assert.Empty(license.Seats);
        Assert.Equal(logCountAfterFirstRun, activity.Logs.Count);
    }

    [Fact]
    public async Task RunAsync_NeverChangesAssetStatus()
    {
        var (service, clock, organizations, people, _, _) = CreateService();
        var org = CreateOrganization();
        organizations.Add(org);
        var person = new Person(org.Id, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(clock.UtcNow.AddDays(-1));
        people.Add(person);
        var asset = new Asset(org.Id, Guid.NewGuid(), "Laptop", "AT-1");
        asset.AssignTo(person.Id);

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(AssetStatus.Assigned, asset.Status);
        Assert.Equal(person.Id, asset.AssignedPersonId);
    }

    [Fact]
    public async Task RunAsync_IsTenantIsolated()
    {
        var (service, clock, organizations, people, licenses, _) = CreateService();
        var orgA = CreateOrganization();
        var orgB = new Organization("Beta", "PL", "pl", "PLN", "Europe/Warsaw");
        organizations.Add(orgA);
        organizations.Add(orgB);

        var personA = new Person(orgA.Id, "Jan", "Kowalski", "jan@acme.test");
        personA.StartOffboarding(clock.UtcNow.AddDays(-1));
        people.Add(personA);

        var personB = new Person(orgB.Id, "Anna", "Nowak", "anna@beta.test");
        people.Add(personB);

        var licenseB = new License(orgB.Id, "Beta Suite", null, null, 5, null, null);
        licenseB.AssignSeat(personA.Id, clock.UtcNow);
        licenses.Add(licenseB);

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(EmploymentStatus.Inactive, personA.EmploymentStatus);
        Assert.Equal(EmploymentStatus.Active, personB.EmploymentStatus);
        Assert.Single(licenseB.Seats);
    }
}
