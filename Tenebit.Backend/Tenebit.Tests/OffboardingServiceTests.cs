using Tenebit.Application.Offboarding;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class OffboardingServiceTests
{
    private static (OffboardingService Service, FakeCurrentUser User, InMemoryOffboardingCaseRepository Cases, InMemoryOffboardingItemRepository Items,
        InMemoryPersonRepository People, InMemoryAssetRepository Assets, InMemoryAssignmentRepository Assignments, InMemoryLicenseRepository Licenses,
        InMemoryActivityLogRepository Activity) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var cases = new InMemoryOffboardingCaseRepository();
        var items = new InMemoryOffboardingItemRepository();
        var people = new InMemoryPersonRepository();
        var assets = new InMemoryAssetRepository();
        var assignments = new InMemoryAssignmentRepository();
        var licenses = new InMemoryLicenseRepository();
        var activity = new InMemoryActivityLogRepository();

        var service = new OffboardingService(cases, items, people, assets, assignments, licenses, activity, currentUser, new FakeClock(), new FakeUnitOfWork(),
            new OffboardingScheduledActionsService(cases, items, licenses, activity, new FakeUnitOfWork()));

        return (service, currentUser, cases, items, people, assets, assignments, licenses, activity);
    }

    private static Person AddPerson(FakeCurrentUser user, InMemoryPersonRepository people, string email = "jan.kowalski@acme.test")
    {
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", email);
        people.Add(person);
        return person;
    }

    private static Asset AddAsset(FakeCurrentUser user, InMemoryAssetRepository assets, Guid? assignedPersonId = null)
    {
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        if (assignedPersonId.HasValue) asset.AssignTo(assignedPersonId.Value);
        assets.Add(asset);
        return asset;
    }

    [Fact]
    public async Task CreateAsync_CreatesDraftCase()
    {
        var (service, user, _, _, people, _, _, _, activity) = CreateService();
        var person = AddPerson(user, people);

        var result = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, true, true, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OffboardingCaseStatus.Draft, result.Value!.Case.Status);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.created");
    }

    [Fact]
    public async Task CreateAsync_RejectsSecondOpenCaseForSamePerson_WithConflictNotException()
    {
        var (service, user, _, _, people, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);

        var first = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow.AddDays(37), null, null, null, false, false, false), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("CONFLICT", second.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_RejectsPersonNotActive()
    {
        var (service, user, _, _, people, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        person.StartOffboarding(DateTimeOffset.UtcNow.AddDays(1));

        var result = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task StartAsync_SnapshotsDirectlyAssignedAndOpenAssignmentAssets_AndLicenseSeats()
    {
        var (service, user, cases, items, people, assets, assignments, licenses, activity) = CreateService();
        var person = AddPerson(user, people);

        // Directly assigned asset (e.g. via onboarding starter package, no Assignment record).
        var directAsset = AddAsset(user, assets, person.Id);

        // Asset issued through a formal, still-open Assignment.
        var issuedAsset = AddAsset(user, assets, person.Id);
        var assignment = new Assignment(user.OrganizationId, person.Id, "PROT-1", DateTimeOffset.UtcNow, null, null, user.Subject);
        assignment.AddAsset(issuedAsset.Id, null);
        assignments.Add(assignment);

        var license = new License(user.OrganizationId, "Office 365", null, null, 5, null, null);
        license.AssignSeat(person.Id, DateTimeOffset.UtcNow);
        licenses.Add(license);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, true), CancellationToken.None);
        Assert.True(created.IsSuccess);

        var started = await service.StartAsync(created.Value!.Case.Id, CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Equal(OffboardingCaseStatus.Active, started.Value!.Case.Status);

        var assetItems = started.Value.Items.Where(x => x.Type == OffboardingItemType.AssetReturn).ToList();
        Assert.Equal(2, assetItems.Count);
        Assert.Contains(assetItems, x => x.AssetId == directAsset.Id && x.AssignmentId == null);
        Assert.Contains(assetItems, x => x.AssetId == issuedAsset.Id && x.AssignmentId == assignment.Id);
        Assert.All(assetItems, x => Assert.True(x.Required));

        var licenseItems = started.Value.Items.Where(x => x.Type == OffboardingItemType.LicenseRelease).ToList();
        Assert.Single(licenseItems);
        Assert.Equal(OffboardingItemAutomationMode.AtEmploymentEnd, licenseItems[0].AutomationMode);

        // Assets are marked pending return and the person moves into the Offboarding employment state.
        Assert.Equal(AssetStatus.PendingReturn, assets.Assets.First(x => x.Id == directAsset.Id).Status);
        Assert.Equal(AssetStatus.PendingReturn, assets.Assets.First(x => x.Id == issuedAsset.Id).Status);
        Assert.Equal(EmploymentStatus.Offboarding, people.People.First(x => x.Id == person.Id).EmploymentStatus);

        Assert.Contains(activity.Logs, x => x.Action == "offboarding.started");
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.person_marked_offboarding");
        Assert.Equal(2, activity.Logs.Count(x => x.Action == "offboarding.asset_marked_pending_return"));
    }

    [Fact]
    public async Task StartAsync_WorksForPersonWithoutOwnEmailNotification()
    {
        // The service never reads/sends email during Start — it only touches domain state and items,
        // so the result is identical regardless of the person's email content.
        var (service, user, _, _, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people, email: "no-reply@acme.test");
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Single(started.Value!.Items);
    }

    [Fact]
    public async Task ListPagedAsync_OnlyReturnsCasesForCurrentOrganization()
    {
        var (service, user, cases, _, people, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);

        var otherOrgPerson = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        cases.Cases.Add(new OffboardingCase(otherOrgPerson.OrganizationId, otherOrgPerson.Id, DateTimeOffset.UtcNow.AddDays(10), DateTimeOffset.UtcNow.AddDays(17), null, null, null, false, false, false, "system", DateTimeOffset.UtcNow));

        var result = await service.ListPagedAsync(null, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
    }

    [Fact]
    public async Task UpdateAsync_AllowedOnlyInDraft()
    {
        var (service, user, _, _, people, assets, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, CancellationToken.None);

        var update = await service.UpdateAsync(created.Value.Case.Id, new UpdateOffboardingCaseRequest(DateTimeOffset.UtcNow.AddDays(15), DateTimeOffset.UtcNow.AddDays(22), null, null, null, false, false, false), CancellationToken.None);

        Assert.True(update.IsFailure);
    }

    [Fact]
    public async Task ExecuteScheduledActionsAsync_PastEmploymentEndsAt_ImmediatelyDeactivatesAndReleasesLicenses()
    {
        var (service, user, _, _, people, assets, _, licenses, activity) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var license = new License(user.OrganizationId, "Office 365", null, null, 5, null, null);
        license.AssignSeat(person.Id, DateTimeOffset.UtcNow);
        licenses.Add(license);

        // Employment end date is already in the past when the case is started.
        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3), null, null, null, false, false, true), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, CancellationToken.None);

        var result = await service.ExecuteScheduledActionsAsync(created.Value.Case.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmploymentStatus.Inactive, people.People.First(x => x.Id == person.Id).EmploymentStatus);
        Assert.Empty(license.Seats);
        Assert.Contains(result.Value!.Items, x => x.Type == OffboardingItemType.LicenseRelease && x.Status == OffboardingItemStatus.Released);
        Assert.Contains(activity.Logs, x => x.Action == "person.deactivated");
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.license_released");
    }
}
