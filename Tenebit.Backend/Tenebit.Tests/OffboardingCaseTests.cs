using Tenebit.Application.Assets;
using Tenebit.Application.Offboarding;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Common;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;
using Tenebit.Domain.Reservations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class OffboardingCaseTests
{
    private static (OffboardingService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryAssetRepository Assets,
        InMemoryAssignmentRepository Assignments, InMemoryLicenseRepository Licenses, InMemoryEquipmentReservationRepository Reservations,
        InMemoryAssetAuditCampaignRepository AuditCampaigns, InMemoryAssetAuditItemRepository AuditItems, FakeClock Clock) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var cases = new InMemoryOffboardingCaseRepository();
        var items = new InMemoryOffboardingItemRepository();
        var people = new InMemoryPersonRepository();
        var assets = new InMemoryAssetRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var assignments = new InMemoryAssignmentRepository();
        var licenses = new InMemoryLicenseRepository();
        var activity = new InMemoryActivityLogRepository();
        var inspections = new InMemoryAssetInspectionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock();
        var reservations = new InMemoryEquipmentReservationRepository();
        var auditCampaigns = new InMemoryAssetAuditCampaignRepository();
        var auditItems = new InMemoryAssetAuditItemRepository();

        var inspectionService = new AssetInspectionService(inspections, assets, activity, currentUser, clock, unitOfWork, TestAuthorization.Asset(assets, currentUser));
        var disposition = new AssetReturnDispositionService(inspections);
        var evidence = new InMemoryAssetEvidenceRepository();
        var evidenceService = new Tenebit.Application.Evidence.AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, currentUser, clock, unitOfWork, TestAuthorization.Asset(assets, currentUser));
        var organizations = new InMemoryOrganizationRepository();
        var responseBuilder = new OffboardingResponseBuilder(cases, items, people, organizations, assets, evidence, reservations, clock);

        var service = new OffboardingService(cases, items, people, assets, categories, assignments, licenses, activity, currentUser, clock, unitOfWork,
            new OffboardingScheduledActionsService(cases, items, licenses, activity, new FakeUnitOfWork()), disposition, inspectionService, inspections,
            organizations, new FakeEmailSender(), new FakeAppLinkBuilder(), evidenceService,
            reservations, auditCampaigns, auditItems, responseBuilder);

        return (service, currentUser, people, assets, assignments, licenses, reservations, auditCampaigns, auditItems, clock);
    }

    private static async Task<OffboardingPreviewResponse> SeedPreviewAsync()
    {
        var (service, user, people, assets, assignments, licenses, reservations, auditCampaigns, auditItems, clock) = CreateService();
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(person);

        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-TEST-01");
        asset.AssignTo(person.Id);
        assets.Add(asset);

        assignments.Add(new Assignment(user.OrganizationId, person.Id, "PROT-1", clock.UtcNow, null, null, "admin@acme.test"));

        var license = new License(user.OrganizationId, "Office", null, null, 2, null, null);
        license.AssignSeat(person.Id, clock.UtcNow);
        licenses.Add(license);

        var pendingReservation = new EquipmentReservation(user.OrganizationId, person.Id, clock.UtcNow.AddDays(3), clock.UtcNow.AddDays(5), "Delegacja", null, null);
        pendingReservation.AddItem(Guid.NewGuid(), 1, null);
        pendingReservation.Submit(clock.UtcNow);
        reservations.Add(pendingReservation);

        var campaign = new AssetAuditCampaign(user.OrganizationId, "Inwentaryzacja", null, clock.UtcNow.AddDays(7), null, "admin@acme.test", clock.UtcNow);
        auditCampaigns.Add(campaign);
        var respondedItem = new AssetAuditItem(user.OrganizationId, campaign.Id, Guid.NewGuid(), asset.Id, person.Id, null);
        respondedItem.RecordResponse(AssetAuditResponse.Confirmed, null, clock.UtcNow);
        auditItems.Add(respondedItem);
        var pendingAuditItem = new AssetAuditItem(user.OrganizationId, campaign.Id, Guid.NewGuid(), asset.Id, person.Id, null);
        auditItems.Add(pendingAuditItem);

        var preview = await service.GetPreviewAsync(person.Id, CancellationToken.None);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        return preview.Value!;
    }

    [Fact]
    public async Task GetPreviewAsync_ReturnsAllCategoriesForSeededPerson()
    {
        var preview = await SeedPreviewAsync();

        Assert.Single(preview.HeldAssets);
        Assert.Equal("Laptop", preview.HeldAssets[0].Name);
        Assert.Single(preview.OpenAssignments);
        Assert.Equal("PROT-1", preview.OpenAssignments[0].ProtocolNumber);
        Assert.Single(preview.LicenseSeats);
        Assert.Equal("Office", preview.LicenseSeats[0].Name);
        Assert.Single(preview.Reservations);
        Assert.Equal(EquipmentReservationStatus.PendingApproval, preview.Reservations[0].Status);
        Assert.Single(preview.UnresolvedAuditItems);
        Assert.Equal(AssetAuditResponse.Confirmed, preview.UnresolvedAuditItems[0].Response);
    }

    [Fact]
    public async Task GetPreviewAsync_ReturnsEmptyListsForPersonWithoutData()
    {
        var (service, user, people, _, _, _, _, _, _, _) = CreateService();
        var person = new Person(user.OrganizationId, "Anna", "Nowak", "anna@acme.test");
        people.Add(person);

        var preview = await service.GetPreviewAsync(person.Id, CancellationToken.None);

        Assert.True(preview.IsSuccess);
        Assert.Empty(preview.Value!.HeldAssets);
        Assert.Empty(preview.Value.OpenAssignments);
        Assert.Empty(preview.Value.LicenseSeats);
        Assert.Empty(preview.Value.Reservations);
        Assert.Empty(preview.Value.UnresolvedAuditItems);
    }

    [Fact]
    public async Task GetPreviewAsync_ReturnsNotFoundForUnknownPerson()
    {
        var (service, _, _, _, _, _, _, _, _, _) = CreateService();

        var preview = await service.GetPreviewAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(preview.IsFailure);
        Assert.Equal("PERSON_NOT_FOUND", preview.Error!.Code);
    }

    [Fact]
    public async Task GetAsync_IncludesPendingAndFutureReservationsAndExcludesOthers()
    {
        var (service, user, people, _, _, _, reservations, _, _, clock) = CreateService();
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(person);
        var now = clock.UtcNow;

        var pending = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(3), now.AddDays(5), "Delegacja", null, null);
        pending.AddItem(Guid.NewGuid(), 1, null);
        pending.Submit(now);
        reservations.Add(pending);

        var approvedFuture = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(10), now.AddDays(12), "Szkolenie", null, null);
        approvedFuture.AddItem(Guid.NewGuid(), 1, null);
        approvedFuture.Submit(now);
        approvedFuture.Approve(now, "admin");
        reservations.Add(approvedFuture);

        var rejected = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(3), now.AddDays(5), "Odrzucona", null, null);
        rejected.AddItem(Guid.NewGuid(), 1, null);
        rejected.Submit(now);
        rejected.Reject(now, "admin", "Poza polityką");
        reservations.Add(rejected);

        var cancelled = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(3), now.AddDays(5), "Anulowana", null, null);
        cancelled.AddItem(Guid.NewGuid(), 1, null);
        cancelled.Submit(now);
        cancelled.Cancel(now, "admin", "Rezygnacja");
        reservations.Add(cancelled);

        clock.UtcNow = now.AddDays(-6);
        var approvedPast = new EquipmentReservation(user.OrganizationId, person.Id, now.AddDays(-5), now.AddDays(-3), "Zakończona", null, null);
        approvedPast.AddItem(Guid.NewGuid(), 1, null);
        approvedPast.Submit(clock.UtcNow);
        approvedPast.Approve(clock.UtcNow, "admin");
        reservations.Add(approvedPast);
        clock.UtcNow = now;

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, now.AddDays(14), now.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var details = await service.GetAsync(created.Value!.Case.Id, CancellationToken.None);

        Assert.True(details.IsSuccess);
        var ids = details.Value!.Reservations.Select(r => r.Id).ToList();
        Assert.Contains(pending.Id, ids);
        Assert.Contains(approvedFuture.Id, ids);
        Assert.DoesNotContain(rejected.Id, ids);
        Assert.DoesNotContain(cancelled.Id, ids);
        Assert.DoesNotContain(approvedPast.Id, ids);
    }

    private static OffboardingCase CreateCase(DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        return new OffboardingCase(Guid.NewGuid(), Guid.NewGuid(), at.AddDays(14), at.AddDays(21),
            "Magazyn główny", "Notatka", null, true, true, true, "admin@acme.test", at);
    }

    private static OffboardingItem CreateRequiredItem(Guid caseId, Guid organizationId) =>
        new(organizationId, caseId, OffboardingItemType.AssetReturn, "Laptop", true, Guid.NewGuid(), null, null, OffboardingItemAutomationMode.Manual, 0);

    [Fact]
    public void Start_TransitionsFromDraftToActive()
    {
        var offboardingCase = CreateCase();

        offboardingCase.Start(DateTimeOffset.UtcNow);

        Assert.Equal(OffboardingCaseStatus.Active, offboardingCase.Status);
        Assert.NotNull(offboardingCase.StartedAt);
    }

    [Fact]
    public void Start_ThrowsWhenNotDraft()
    {
        var offboardingCase = CreateCase();
        offboardingCase.Start(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => offboardingCase.Start(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Complete_ThrowsWhenRequiredItemIsStillOpen()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        offboardingCase.MarkPersonDeactivated(now);
        var openItem = CreateRequiredItem(offboardingCase.Id, offboardingCase.OrganizationId);

        offboardingCase.RecomputeStatus([openItem], now);

        Assert.Throws<DomainException>(() => offboardingCase.Complete(now, "admin@acme.test", "PROT-1"));
    }

    [Fact]
    public void Complete_ThrowsWhenPersonNotDeactivatedDespiteResolvedItems()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        var item = CreateRequiredItem(offboardingCase.Id, offboardingCase.OrganizationId);
        item.MarkReceived(now, "admin@acme.test");
        item.CompleteInspection(now, "admin@acme.test");

        offboardingCase.RecomputeStatus([item], now);

        Assert.Equal(OffboardingCaseStatus.ReadyToClose, offboardingCase.Status);
        Assert.Throws<DomainException>(() => offboardingCase.Complete(now, "admin@acme.test", "PROT-1"));
        Assert.Equal(OffboardingCaseStatus.ReadyToClose, offboardingCase.Status);
    }

    [Fact]
    public void Cancel_SucceedsBeforePersonDeactivation()
    {
        var offboardingCase = CreateCase();
        offboardingCase.Start(DateTimeOffset.UtcNow);

        offboardingCase.Cancel(DateTimeOffset.UtcNow, "Rezygnacja z procesu");

        Assert.Equal(OffboardingCaseStatus.Cancelled, offboardingCase.Status);
        Assert.Equal("Rezygnacja z procesu", offboardingCase.CancellationReason);
    }

    [Fact]
    public void Cancel_RevokesPublicToken()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        offboardingCase.SetPublicToken("hash", now.AddDays(30));

        offboardingCase.Cancel(now, "Rezygnacja z procesu");

        Assert.NotNull(offboardingCase.PublicTokenRevokedAt);
    }

    [Fact]
    public void Cancel_IsBlockedAfterPersonDeactivation()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        offboardingCase.MarkPersonDeactivated(now);

        Assert.Throws<DomainException>(() => offboardingCase.Cancel(now, "Za późno"));
        Assert.Equal(OffboardingCaseStatus.Active, offboardingCase.Status);
    }

    [Fact]
    public void Complete_IsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        var item = CreateRequiredItem(offboardingCase.Id, offboardingCase.OrganizationId);
        item.MarkReceived(now, "admin@acme.test");
        item.CompleteInspection(now, "admin@acme.test");
        offboardingCase.MarkPersonDeactivated(now);
        offboardingCase.RecomputeStatus([item], now);

        offboardingCase.Complete(now, "admin@acme.test", "PROT-1");
        var completedAtFirstCall = offboardingCase.CompletedAt;

        offboardingCase.Complete(now.AddMinutes(5), "someone-else@acme.test", "PROT-2");

        Assert.Equal(OffboardingCaseStatus.Completed, offboardingCase.Status);
        Assert.Equal(completedAtFirstCall, offboardingCase.CompletedAt);
        Assert.Equal("admin@acme.test", offboardingCase.CompletedBy);
        Assert.Equal("PROT-1", offboardingCase.FinalProtocolNumber);
    }

    [Fact]
    public void Complete_SucceedsWhenReadyAndPersonDeactivated()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        var item = CreateRequiredItem(offboardingCase.Id, offboardingCase.OrganizationId);
        item.MarkReceived(now, "admin@acme.test");
        item.CompleteInspection(now, "admin@acme.test");
        offboardingCase.MarkPersonDeactivated(now);
        offboardingCase.RecomputeStatus([item], now);

        offboardingCase.Complete(now, "admin@acme.test", "PROT-1");

        Assert.Equal(OffboardingCaseStatus.Completed, offboardingCase.Status);
        Assert.NotNull(offboardingCase.PublicTokenRevokedAt);
    }

    [Fact]
    public void RestoreEmployment_ThrowsWhenPersonNotDeactivated()
    {
        var offboardingCase = CreateCase();
        offboardingCase.Start(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => offboardingCase.RestoreEmployment(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RestoreEmployment_CancelsCaseAfterDeactivation()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        offboardingCase.MarkPersonDeactivated(now);

        offboardingCase.RestoreEmployment(now);

        Assert.Equal(OffboardingCaseStatus.Cancelled, offboardingCase.Status);
        Assert.Equal("Przywrócenie zatrudnienia", offboardingCase.CancellationReason);
    }

    [Fact]
    public void RestoreEmployment_RevokesPublicToken()
    {
        var now = DateTimeOffset.UtcNow;
        var offboardingCase = CreateCase(now);
        offboardingCase.Start(now);
        offboardingCase.SetPublicToken("hash", now.AddDays(30));
        offboardingCase.MarkPersonDeactivated(now);

        offboardingCase.RestoreEmployment(now);

        Assert.NotNull(offboardingCase.PublicTokenRevokedAt);
    }
}
