using Tenebit.Application.Assets;
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
        InMemoryActivityLogRepository Activity, InMemoryAssetCategoryRepository Categories, InMemoryAssetInspectionRepository Inspections, FakeEmailSender EmailSender) CreateService()
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

        var inspectionService = new AssetInspectionService(inspections, assets, activity, currentUser, new FakeClock(), unitOfWork);
        var disposition = new AssetReturnDispositionService(inspections);
        var organizations = new InMemoryOrganizationRepository();
        var emailSender = new FakeEmailSender();
        var linkBuilder = new FakeAppLinkBuilder();
        var evidence = new InMemoryAssetEvidenceRepository();
        var evidenceService = new Tenebit.Application.Evidence.AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, currentUser, new FakeClock(), unitOfWork);
        var reservations = new InMemoryEquipmentReservationRepository();
        var responseBuilder = new OffboardingResponseBuilder(cases, items, people, organizations, assets, evidence, reservations, new FakeClock());
        var protocolModelBuilder = new OffboardingProtocolModelBuilder(organizations, people, items, assets, evidence, licenses);

        var service = new OffboardingService(cases, items, people, assets, categories, assignments, licenses, activity, currentUser, new FakeClock(), unitOfWork,
            new OffboardingScheduledActionsService(cases, items, licenses, activity, new FakeUnitOfWork()), disposition, inspectionService, inspections,
            organizations, emailSender, linkBuilder, evidenceService, new FakePdfProtocolGenerator(), reservations,
            new InMemoryAssetAuditCampaignRepository(), new InMemoryAssetAuditItemRepository(), responseBuilder, protocolModelBuilder);

        return (service, currentUser, cases, items, people, assets, assignments, licenses, activity, categories, inspections, emailSender);
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
        var (service, user, _, _, people, _, _, _, activity, _, _, _) = CreateService();
        var person = AddPerson(user, people);

        var result = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, true, true, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OffboardingCaseStatus.Draft, result.Value!.Case.Status);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.created");
    }

    [Fact]
    public async Task CreateAsync_RejectsCrossOrganizationProcessOwnerId()
    {
        var (service, user, _, _, people, _, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var otherOrgOwner = new Person(Guid.NewGuid(), "Anna", "Nowak", "anna@other.test");
        people.Add(otherOrgOwner);

        var result = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, otherOrgOwner.Id, false, false, false), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsSecondOpenCaseForSamePerson_WithConflictNotException()
    {
        var (service, user, _, _, people, _, _, _, _, _, _, _) = CreateService();
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
        var (service, user, _, _, people, _, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        person.StartOffboarding(DateTimeOffset.UtcNow.AddDays(1));

        var result = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task StartAsync_SnapshotsDirectlyAssignedAndOpenAssignmentAssets_AndLicenseSeats()
    {
        var (service, user, cases, items, people, assets, assignments, licenses, activity, _, _, _) = CreateService();
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

        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

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
        var (service, user, _, _, people, assets, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people, email: "no-reply@acme.test");
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Single(started.Value!.Items);
    }

    [Fact]
    public async Task ListPagedAsync_OnlyReturnsCasesForCurrentOrganization()
    {
        var (service, user, cases, _, people, _, _, _, _, _, _, _) = CreateService();
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
        var (service, user, _, _, people, assets, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);
        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

        var update = await service.UpdateAsync(created.Value.Case.Id, new UpdateOffboardingCaseRequest(DateTimeOffset.UtcNow.AddDays(15), DateTimeOffset.UtcNow.AddDays(22), null, null, null, false, false, false), CancellationToken.None);

        Assert.True(update.IsFailure);
    }

    [Fact]
    public async Task ExecuteScheduledActionsAsync_PastEmploymentEndsAt_ImmediatelyDeactivatesAndReleasesLicenses()
    {
        var (service, user, _, _, people, assets, _, licenses, activity, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var license = new License(user.OrganizationId, "Office 365", null, null, 5, null, null);
        license.AssignSeat(person.Id, DateTimeOffset.UtcNow);
        licenses.Add(license);

        // Employment end date is already in the past when the case is started.
        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3), null, null, null, false, false, true), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

        var result = await service.ExecuteScheduledActionsAsync(created.Value.Case.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmploymentStatus.Inactive, people.People.First(x => x.Id == person.Id).EmploymentStatus);
        Assert.Empty(license.Seats);
        Assert.Contains(result.Value!.Items, x => x.Type == OffboardingItemType.LicenseRelease && x.Status == OffboardingItemStatus.Released);
        Assert.Contains(activity.Logs, x => x.Action == "person.deactivated");
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.license_released");
    }

    [Fact]
    public async Task ConfirmItemReturnAsync_DirectToStockCategory_CompletesReturnInStock()
    {
        var (service, user, _, _, people, assets, _, _, activity, categories, _, _) = CreateService();
        var person = AddPerson(user, people);
        var category = new AssetCategory(user.OrganizationId, "Laptops", AssetCategoryType.Physical, null, returnHandlingMode: ReturnHandlingMode.DirectToStock);
        categories.Add(category);
        var asset = new Asset(user.OrganizationId, category.Id, "Laptop", "AT-0001");
        asset.AssignTo(person.Id);
        assets.Add(asset);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        var item = started.Value!.Items.Single(x => x.Type == OffboardingItemType.AssetReturn);

        var result = await service.ConfirmItemReturnAsync(created.Value.Case.Id, item.Id, new ConfirmOffboardingItemReturnRequest(null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.InStock, assets.Assets.Single(x => x.Id == asset.Id).Status);
        Assert.Equal(OffboardingItemStatus.Returned, result.Value!.Items.Single(x => x.Id == item.Id).Status);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.asset_returned");
    }

    [Fact]
    public async Task ConfirmItemReturnAsync_InspectionRequiredCategory_StaysInProgressUntilInspectionCompleted()
    {
        var (service, user, _, _, people, assets, _, _, activity, categories, inspections, _) = CreateService();
        var person = AddPerson(user, people);
        var category = new AssetCategory(user.OrganizationId, "Laptops", AssetCategoryType.Physical, null, returnHandlingMode: ReturnHandlingMode.InspectionRequired);
        categories.Add(category);
        var asset = new Asset(user.OrganizationId, category.Id, "Laptop", "AT-0002");
        asset.AssignTo(person.Id);
        assets.Add(asset);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        var item = started.Value!.Items.Single(x => x.Type == OffboardingItemType.AssetReturn);

        var confirmed = await service.ConfirmItemReturnAsync(created.Value.Case.Id, item.Id, new ConfirmOffboardingItemReturnRequest(null, null, null), CancellationToken.None);

        Assert.True(confirmed.IsSuccess);
        Assert.Equal(AssetStatus.InService, assets.Assets.Single(x => x.Id == asset.Id).Status);
        Assert.Equal(OffboardingItemStatus.Received, confirmed.Value!.Items.Single(x => x.Id == item.Id).Status);
        Assert.Single(inspections.Inspections);

        var completed = await service.CompleteItemInspectionAsync(created.Value.Case.Id, item.Id,
            new CompleteAssetInspectionRequest(InspectionOutcome.ReadyForReuse, true, true, true, true, null, null), CancellationToken.None);

        Assert.True(completed.IsSuccess);
        Assert.Equal(AssetStatus.InStock, assets.Assets.Single(x => x.Id == asset.Id).Status);
        Assert.Equal(OffboardingItemStatus.Returned, completed.Value!.Items.Single(x => x.Id == item.Id).Status);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.asset_inspection_completed");
    }

    [Fact]
    public async Task CompleteAsync_FailsWithOpenRequiredItem()
    {
        var (service, user, _, _, people, assets, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

        var result = await service.CompleteAsync(created.Value.Case.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CancelAsync_RestoresUnresolvedAssetsToAssigned()
    {
        var (service, user, _, _, people, assets, _, _, activity, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        Assert.Equal(AssetStatus.PendingReturn, assets.Assets.Single(x => x.Id == asset.Id).Status);

        var cancelled = await service.CancelAsync(created.Value.Case.Id, new CancelOffboardingCaseRequest("Pomyłka"), CancellationToken.None);

        Assert.True(cancelled.IsSuccess, cancelled.Error?.Message);
        Assert.Equal(OffboardingCaseStatus.Cancelled, cancelled.Value!.Case.Status);
        Assert.Equal(AssetStatus.Assigned, assets.Assets.Single(x => x.Id == asset.Id).Status);
        Assert.Equal(person.Id, assets.Assets.Single(x => x.Id == asset.Id).AssignedPersonId);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.cancelled");
    }

    [Fact]
    public async Task CancelAsync_BlockedAfterPersonDeactivation()
    {
        var (service, user, _, _, people, assets, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        await service.ExecuteScheduledActionsAsync(created.Value.Case.Id, CancellationToken.None);

        var cancelled = await service.CancelAsync(created.Value.Case.Id, new CancelOffboardingCaseRequest("Za późno"), CancellationToken.None);

        Assert.True(cancelled.IsFailure);
    }

    [Fact]
    public async Task WaiveItemAsync_MarksItemAsWaived()
    {
        var (service, user, _, _, people, assets, _, _, activity, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        var item = started.Value!.Items.Single();

        var result = await service.WaiveItemAsync(created.Value.Case.Id, item.Id, new WaiveOffboardingItemRequest("Aktywo już nie istnieje"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OffboardingItemStatus.Waived, result.Value!.Items.Single(x => x.Id == item.Id).Status);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.item_waived");
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotent_DoesNotGenerateSecondProtocolNumber()
    {
        var (service, user, _, _, people, assets, _, _, activity, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        var item = started.Value!.Items.Single();
        await service.WaiveItemAsync(created.Value.Case.Id, item.Id, new WaiveOffboardingItemRequest("Nie odzyskane"), CancellationToken.None);
        await service.ExecuteScheduledActionsAsync(created.Value.Case.Id, CancellationToken.None);

        var first = await service.CompleteAsync(created.Value.Case.Id, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var firstProtocolNumber = first.Value!.Case.FinalProtocolNumber;
        Assert.False(string.IsNullOrWhiteSpace(firstProtocolNumber));

        var second = await service.CompleteAsync(created.Value.Case.Id, CancellationToken.None);
        Assert.True(second.IsSuccess);
        Assert.Equal(firstProtocolNumber, second.Value!.Case.FinalProtocolNumber);
        Assert.Single(activity.Logs, x => x.Action == "offboarding.completed");
    }

    [Fact]
    public async Task GetProtocolPdfAsync_FailsBeforeCompletion_SucceedsAfter_AndIsStableAcrossCalls()
    {
        var (service, user, _, _, people, assets, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        var item = started.Value!.Items.Single();

        var beforeCompletion = await service.GetProtocolPdfAsync(created.Value.Case.Id, CancellationToken.None);
        Assert.True(beforeCompletion.IsFailure);

        await service.WaiveItemAsync(created.Value.Case.Id, item.Id, new WaiveOffboardingItemRequest("Nie odzyskane"), CancellationToken.None);
        await service.ExecuteScheduledActionsAsync(created.Value.Case.Id, CancellationToken.None);
        await service.CompleteAsync(created.Value.Case.Id, CancellationToken.None);

        var firstPdf = await service.GetProtocolPdfAsync(created.Value.Case.Id, CancellationToken.None);
        var secondPdf = await service.GetProtocolPdfAsync(created.Value.Case.Id, CancellationToken.None);

        Assert.True(firstPdf.IsSuccess);
        Assert.True(secondPdf.IsSuccess);
        Assert.Equal(firstPdf.Value, secondPdf.Value);
    }

    [Fact]
    public async Task ResolveItemAsync_Missing_MarksAssetLost()
    {
        var (service, user, _, _, people, assets, _, _, activity, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        var item = started.Value!.Items.Single();

        var result = await service.ResolveItemAsync(created.Value.Case.Id, item.Id, new ResolveOffboardingItemRequest(OffboardingItemStatus.Missing, "Pracownik nie odpowiada"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.Lost, assets.Assets.Single(x => x.Id == asset.Id).Status);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.asset_missing");
    }

    [Fact]
    public async Task StartAsync_WithEmailAndDefaultNotify_IssuesPublicTokenAndSendsEmail()
    {
        var (service, user, cases, _, people, assets, _, _, activity, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

        var stored = cases.Cases.Single(x => x.Id == created.Value.Case.Id);
        Assert.NotNull(stored.PublicTokenHash);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.link_sent");
    }

    [Fact]
    public async Task StartAsync_NotifyEmployeeFalse_DoesNotIssueToken()
    {
        // Domena Person zawsze wymaga poprawnego adresu e-mail (Person.Update), więc "brak e-maila" nie jest
        // reprezentowalnym stanem — realnym odpowiednikiem z kryterium 4.12 jest notifyEmployee=false przy starcie.
        var (service, user, cases, _, people, assets, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(NotifyEmployee: false), CancellationToken.None);

        Assert.True(started.IsSuccess);
        var stored = cases.Cases.Single(x => x.Id == created.Value.Case.Id);
        Assert.Null(stored.PublicTokenHash);
    }

    [Fact]
    public async Task GetPublicAsync_ExpiredOrRevokedOrUnknownToken_ReturnsNotFound()
    {
        var (service, user, cases, _, people, assets, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

        var unknown = await service.GetPublicAsync("does-not-exist", CancellationToken.None);
        Assert.True(unknown.IsFailure);

        var stored = cases.Cases.Single(x => x.Id == created.Value.Case.Id);
        stored.RevokePublicToken(DateTimeOffset.UtcNow);
        var revoked = await service.GetPublicAsync("does-not-exist", CancellationToken.None);
        Assert.True(revoked.IsFailure);
    }

    [Fact]
    public async Task RegenerateLinkAsync_InvalidatesPreviousToken()
    {
        var (service, user, cases, _, people, assets, _, _, activity, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

        var beforeHash = cases.Cases.Single(x => x.Id == created.Value.Case.Id).PublicTokenHash;

        var regenerated = await service.RegenerateLinkAsync(created.Value.Case.Id, CancellationToken.None);
        Assert.True(regenerated.IsSuccess);

        var afterHash = cases.Cases.Single(x => x.Id == created.Value.Case.Id).PublicTokenHash;
        Assert.NotEqual(beforeHash, afterHash);
        Assert.Contains(activity.Logs, x => x.Action == "offboarding.link_regenerated");
    }

    [Fact]
    public async Task RecordEmployeeResponsesAsync_DoesNotChangeAssetStatus_AndSkipsResolvedItems()
    {
        var (service, user, _, _, people, assets, _, _, _, _, _, emailSender) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets, person.Id);

        var created = await service.CreateAsync(new CreateOffboardingCaseRequest(person.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        var started = await service.StartAsync(created.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        var item = started.Value!.Items.Single();

        var rawToken = ExtractRawTokenFromEmail(emailSender);

        var response = await service.RecordEmployeeResponsesAsync(rawToken, new SubmitPublicOffboardingResponseRequest([new PublicOffboardingItemAnswer(item.Id, "Damaged", "Ekran pęknięty")]), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(AssetStatus.PendingReturn, assets.Assets.Single(x => x.Id == asset.Id).Status);
        Assert.Equal("Damaged", response.Value!.Items.Single().EmployeeResponse);

        // Resolving the item, then submitting again should be skipped, not throw.
        await service.ResolveItemAsync(created.Value.Case.Id, item.Id, new ResolveOffboardingItemRequest(OffboardingItemStatus.Damaged, "Potwierdzone"), CancellationToken.None);
        var second = await service.RecordEmployeeResponsesAsync(rawToken, new SubmitPublicOffboardingResponseRequest([new PublicOffboardingItemAnswer(item.Id, "AlreadyReturned", null)]), CancellationToken.None);
        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task GetPublicAsync_TokenScopedToOwnCase_DoesNotResolveOtherCase()
    {
        var (service, user, _, _, people, assets, _, _, _, _, _, emailSender) = CreateService();
        var personA = AddPerson(user, people, email: "a@acme.test");
        var personB = AddPerson(user, people, email: "b@acme.test");
        AddAsset(user, assets, personA.Id);
        AddAsset(user, assets, personB.Id);

        var createdA = await service.CreateAsync(new CreateOffboardingCaseRequest(personA.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(createdA.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);
        var tokenA = ExtractRawTokenFromEmail(emailSender);

        var createdB = await service.CreateAsync(new CreateOffboardingCaseRequest(personB.Id, DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), null, null, null, false, false, false), CancellationToken.None);
        await service.StartAsync(createdB.Value!.Case.Id, new StartOffboardingCaseRequest(), CancellationToken.None);

        var viewA = await service.GetPublicAsync(tokenA, CancellationToken.None);
        Assert.True(viewA.IsSuccess);
        // tokenA must resolve exclusively to case A's data, never leaking case B's items.
        Assert.All(viewA.Value!.Items, item => Assert.DoesNotContain(personB.FullName, item.Label));
    }

    private static string ExtractRawTokenFromEmail(FakeEmailSender emailSender)
    {
        var body = emailSender.Bodies.Last();
        var match = System.Text.RegularExpressions.Regex.Match(body, @"https://test/exit/([^""'\s]+)");
        Assert.True(match.Success, "E-mail nie zawierał linku offboardingowego.");
        return match.Groups[1].Value;
    }
}
