using Tenebit.Application.Offboarding;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

/// <summary>Covers spec 4.5 step 9 / 4.12: scheduled deactivation + license release must isolate per-license
/// failures (one bad license must not roll back the person's deactivation nor other successful releases),
/// must be idempotent across repeated runs, and ScheduledActionsCompletedAt must only be set once every
/// AtEmploymentEnd item has actually succeeded (not merely been attempted).</summary>
public class OffboardingScheduledActionsServiceTests
{
    private static (OffboardingScheduledActionsService Service, InMemoryOffboardingCaseRepository Cases, InMemoryOffboardingItemRepository Items, InMemoryLicenseRepository Licenses, InMemoryActivityLogRepository Activity) CreateService()
    {
        var cases = new InMemoryOffboardingCaseRepository();
        var items = new InMemoryOffboardingItemRepository();
        var licenses = new InMemoryLicenseRepository();
        var activity = new InMemoryActivityLogRepository();
        var service = new OffboardingScheduledActionsService(cases, items, licenses, activity, new FakeUnitOfWork());
        return (service, cases, items, licenses, activity);
    }

    [Fact]
    public async Task ExecuteAsync_DeactivatesPersonWithoutAnyEmployeeResponse_AndIsIdempotent()
    {
        var (service, cases, items, licenses, activity) = CreateService();
        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var person = new Person(organizationId, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(now.AddDays(-1));

        var offboardingCase = new OffboardingCase(organizationId, person.Id, now.AddDays(-1), now.AddDays(6), null, null, null, false, false, false, "system", now.AddDays(-10));
        offboardingCase.Start(now.AddDays(-10));
        cases.Add(offboardingCase);

        await service.ExecuteAsync(organizationId, person, now, "system", CancellationToken.None);

        Assert.Equal(EmploymentStatus.Inactive, person.EmploymentStatus);
        Assert.NotNull(offboardingCase.PersonDeactivatedAt);
        // No AtEmploymentEnd items exist for this case -> nothing to schedule, so the marker is set right away.
        Assert.NotNull(offboardingCase.ScheduledActionsCompletedAt);
        var logCountAfterFirstRun = activity.Logs.Count;

        await service.ExecuteAsync(organizationId, person, now.AddMinutes(1), "system", CancellationToken.None);

        Assert.Equal(EmploymentStatus.Inactive, person.EmploymentStatus);
        Assert.Equal(logCountAfterFirstRun, activity.Logs.Count);
    }

    [Fact]
    public async Task ExecuteAsync_OneFailingLicense_DoesNotRollBackDeactivationOrOtherSuccessfulReleases()
    {
        var (service, cases, items, licenses, activity) = CreateService();
        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var person = new Person(organizationId, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(now.AddDays(-1));

        var offboardingCase = new OffboardingCase(organizationId, person.Id, now.AddDays(-1), now.AddDays(6), null, null, null, false, false, true, "system", now.AddDays(-10));
        offboardingCase.Start(now.AddDays(-10));
        cases.Add(offboardingCase);

        var goodLicense = new License(organizationId, "Office 365", null, null, 5, null, null);
        goodLicense.AssignSeat(person.Id, now.AddDays(-30));
        licenses.Add(goodLicense);

        var goodItem = new OffboardingItem(organizationId, offboardingCase.Id, OffboardingItemType.LicenseRelease, "Office 365", false, null, null, goodLicense.Id, OffboardingItemAutomationMode.AtEmploymentEnd, 0);
        items.Add(goodItem);

        // Simulates a license that vanished/is otherwise broken by pointing at an id no longer in the repository.
        var missingLicenseId = Guid.NewGuid();
        var badItem = new OffboardingItem(organizationId, offboardingCase.Id, OffboardingItemType.LicenseRelease, "Broken license", false, null, null, missingLicenseId, OffboardingItemAutomationMode.AtEmploymentEnd, 1);
        items.Add(badItem);

        await service.ExecuteAsync(organizationId, person, now, "system", CancellationToken.None);

        Assert.Equal(EmploymentStatus.Inactive, person.EmploymentStatus);
        Assert.NotNull(offboardingCase.PersonDeactivatedAt);
        Assert.Empty(goodLicense.Seats);
        Assert.Equal(OffboardingItemStatus.Released, goodItem.Status);
        Assert.NotEqual(OffboardingItemStatus.Released, badItem.Status);
        Assert.NotNull(badItem.AutomationError);
        // Not all AtEmploymentEnd items succeeded yet - the marker must stay unset so a future run retries.
        Assert.Null(offboardingCase.ScheduledActionsCompletedAt);

        Assert.Contains(activity.Logs, l => l.Action == "offboarding.license_released" && l.EntityId == goodItem.Id);
        Assert.Contains(activity.Logs, l => l.Action == "offboarding.license_release_failed" && l.EntityId == badItem.Id);
    }

    [Fact]
    public async Task ExecuteAsync_LicenseRelease_IsIdempotent()
    {
        var (service, cases, items, licenses, activity) = CreateService();
        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var person = new Person(organizationId, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(now.AddDays(-1));

        var offboardingCase = new OffboardingCase(organizationId, person.Id, now.AddDays(-1), now.AddDays(6), null, null, null, false, false, true, "system", now.AddDays(-10));
        offboardingCase.Start(now.AddDays(-10));
        cases.Add(offboardingCase);

        var license = new License(organizationId, "Office 365", null, null, 5, null, null);
        license.AssignSeat(person.Id, now.AddDays(-30));
        licenses.Add(license);

        var item = new OffboardingItem(organizationId, offboardingCase.Id, OffboardingItemType.LicenseRelease, "Office 365", false, null, null, license.Id, OffboardingItemAutomationMode.AtEmploymentEnd, 0);
        items.Add(item);

        await service.ExecuteAsync(organizationId, person, now, "system", CancellationToken.None);
        Assert.Equal(OffboardingItemStatus.Released, item.Status);
        Assert.NotNull(offboardingCase.ScheduledActionsCompletedAt);
        var logCountAfterFirstRun = activity.Logs.Count;

        // A second run must not try to re-release an already-terminal item (no duplicate seat unassign attempt/log).
        await service.ExecuteAsync(organizationId, person, now.AddMinutes(1), "system", CancellationToken.None);

        Assert.Equal(logCountAfterFirstRun, activity.Logs.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ManualTaskLicenseItems_AreNotAutomaticallyReleased()
    {
        var (service, cases, items, licenses, activity) = CreateService();
        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var person = new Person(organizationId, "Jan", "Kowalski", "jan@acme.test");
        person.StartOffboarding(now.AddDays(-1));

        var offboardingCase = new OffboardingCase(organizationId, person.Id, now.AddDays(-1), now.AddDays(6), null, null, null, false, false, false, "system", now.AddDays(-10));
        offboardingCase.Start(now.AddDays(-10));
        cases.Add(offboardingCase);

        var license = new License(organizationId, "Office 365", null, null, 5, null, null);
        license.AssignSeat(person.Id, now.AddDays(-30));
        licenses.Add(license);

        var manualItem = new OffboardingItem(organizationId, offboardingCase.Id, OffboardingItemType.LicenseRelease, "Office 365", false, null, null, license.Id, OffboardingItemAutomationMode.Manual, 0);
        items.Add(manualItem);

        await service.ExecuteAsync(organizationId, person, now, "system", CancellationToken.None);

        Assert.Equal(OffboardingItemStatus.Pending, manualItem.Status);
        Assert.Single(license.Seats);
        // Manual case has no AtEmploymentEnd items, so there's nothing scheduled to complete.
        Assert.NotNull(offboardingCase.ScheduledActionsCompletedAt);
    }
}
