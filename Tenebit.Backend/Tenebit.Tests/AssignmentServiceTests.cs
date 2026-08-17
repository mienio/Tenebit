using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Evidence;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssignmentServiceTests
{
    private static (AssignmentService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemoryPersonRepository People, InMemoryProcedureRepository Procedures, InMemoryAssetCategoryRepository Categories, InMemoryAssetInspectionRepository Inspections, InMemoryAssignmentRepository Assignments, InMemoryActivityLogRepository Activity, InMemoryEquipmentReservationRepository Reservations) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var inspections = new InMemoryAssetInspectionRepository();
        var people = new InMemoryPersonRepository();
        var procedures = new InMemoryProcedureRepository();
        var teams = new InMemoryTeamRepository();
        var organizations = new InMemoryOrganizationRepository();
        var activity = new InMemoryActivityLogRepository();
        var assignments = new InMemoryAssignmentRepository();
        var reservations = new InMemoryEquipmentReservationRepository();
        var evidence = new InMemoryAssetEvidenceRepository();
        var clock = new FakeClock();
        var unitOfWork = new FakeUnitOfWork();
        var evidenceService = new AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, currentUser, clock, unitOfWork);
        var disposition = new AssetReturnDispositionService(inspections);
        var responseBuilder = new AssignmentResponseBuilder(assignments, people, assets, procedures, evidence, organizations);
        var protocolModelBuilder = new AssignmentProtocolModelBuilder(assignments, people, teams, organizations, assets, procedures);

        var service = new AssignmentService(
            assignments,
            assets,
            categories,
            inspections,
            people,
            procedures,
            teams,
            organizations,
            activity,
            currentUser,
            clock,
            unitOfWork,
            new FakePdfProtocolGenerator(),
            new FakeEmailSender(),
            new FakeAppLinkBuilder(),
            reservations,
            evidence,
            evidenceService,
            disposition,
            responseBuilder,
            protocolModelBuilder);

        return (service, currentUser, assets, people, procedures, categories, inspections, assignments, activity, reservations);
    }

    private static Person AddPerson(FakeCurrentUser user, InMemoryPersonRepository people)
    {
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan.kowalski@acme.test");
        people.Add(person);
        return person;
    }

    private static Asset AddAsset(FakeCurrentUser user, InMemoryAssetRepository assets)
    {
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        assets.Add(asset);
        return asset;
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyAssetList()
    {
        var (service, user, _, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);

        var result = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [], [], null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsAssetThatIsAlreadyAssigned()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);
        asset.AssignTo(Guid.NewGuid());

        var result = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(EmploymentStatus.Offboarding)]
    [InlineData(EmploymentStatus.Inactive)]
    public async Task CreateAsync_RejectsPersonWhoIsNotActive(EmploymentStatus status)
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        if (status == EmploymentStatus.Offboarding) person.StartOffboarding(DateTimeOffset.UtcNow.AddDays(7));
        else person.Deactivate(DateTimeOffset.UtcNow);
        var asset = AddAsset(user, assets);

        var result = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ASSIGNMENT_PERSON_NOT_ACTIVE", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_DoesNotResolvePersonFromAnotherOrganization()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var otherPerson = new Person(Guid.NewGuid(), "Jan", "Kowalski", "jan@other.test");
        people.Add(otherPerson);
        var asset = AddAsset(user, assets);

        var result = await service.CreateAsync(new CreateAssignmentRequest(otherPerson.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_OnlyRequiresAcceptanceForPublishedProcedures()
    {
        var (service, user, assets, people, procedures, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var draftProcedure = new Procedure(user.OrganizationId, "Polityka bezpieczeństwa", "1.0", "IT", requiresAcceptance: true);
        procedures.Add(draftProcedure);

        var publishedProcedure = new Procedure(user.OrganizationId, "Regulamin pracy", "1.0", "HR", requiresAcceptance: true);
        publishedProcedure.AttachDocument("regulamin.pdf", "application/pdf", [1, 2, 3], "tester", DateTimeOffset.UtcNow);
        publishedProcedure.Publish(DateTimeOffset.UtcNow);
        procedures.Add(publishedProcedure);

        var request = new CreateAssignmentRequest(
            person.Id,
            [new AssignmentAssetRequest(asset.Id, "stan dobry")],
            [draftProcedure.Id, publishedProcedure.Id],
            null,
            null);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var acceptances = result.Value!.ProcedureAcceptances;
        Assert.Single(acceptances);
        Assert.Equal(publishedProcedure.Id, acceptances[0].ProcedureId);
    }

    [Fact]
    public async Task AcceptAsync_TransitionsFromAwaitingAcceptanceToAccepted()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        Assert.True(created.IsSuccess);

        var accepted = await service.AcceptAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(AssignmentStatus.Accepted, accepted.Value!.Status);
    }

    [Fact]
    public async Task AcceptAsync_CannotAcceptAlreadyReturnedAssignment()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        await service.AcceptAsync(created.Value!.Id, CancellationToken.None);
        await service.ReturnAsync(created.Value!.Id, new ReturnAssignmentRequest("ok", null), CancellationToken.None);

        var result = await service.AcceptAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AcceptAsync_StampsTamperEvidentIpAndHash()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        var accepted = await service.AcceptAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(user.IpAddress, accepted.Value!.AcceptedIp);
        Assert.False(string.IsNullOrWhiteSpace(accepted.Value!.AcceptanceHash));
        Assert.True(accepted.Value!.IsIntegrityVerified);
    }

    [Fact]
    public void Assignment_CannotBeAcceptedTwice()
    {
        var organizationId = Guid.NewGuid();
        var assignment = new Assignment(organizationId, Guid.NewGuid(), "TEN-TEST-1", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(Guid.NewGuid(), "ok");
        assignment.Accept(DateTimeOffset.UtcNow, "1.2.3.4");

        Assert.Throws<DomainException>(() => assignment.Accept(DateTimeOffset.UtcNow, "5.6.7.8"));
    }

    [Fact]
    public void Assignment_VerifyIntegrity_DetectsTamperingAfterAcceptance()
    {
        var organizationId = Guid.NewGuid();
        var assignment = new Assignment(organizationId, Guid.NewGuid(), "TEN-TEST-2", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(Guid.NewGuid(), "ok");
        assignment.Accept(DateTimeOffset.UtcNow, "1.2.3.4");

        Assert.True(assignment.VerifyIntegrity());

        var tamperedHashField = typeof(Assignment).GetProperty(nameof(Assignment.AcceptanceHash))!;
        tamperedHashField.SetValue(assignment, "tampered-hash");

        Assert.False(assignment.VerifyIntegrity());
    }

    [Fact]
    public void Assignment_ReturnAsset_IsIdempotent()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-TEST-3", DateTimeOffset.UtcNow, null, null, "tester");
        var assetId = Guid.NewGuid();
        assignment.AddAsset(assetId, "ok");

        assignment.ReturnAsset(assetId, ReturnResolution.Returned, DateTimeOffset.UtcNow, "ok", null, "tester", null);
        var firstReturnedAt = assignment.Assets.Single().ReturnedAt;

        assignment.ReturnAsset(assetId, ReturnResolution.Damaged, DateTimeOffset.UtcNow.AddMinutes(1), "zniszczone", null, "tester", null);

        Assert.Equal(ReturnResolution.Returned, assignment.Assets.Single().ReturnResolution);
        Assert.Equal(firstReturnedAt, assignment.Assets.Single().ReturnedAt);
    }

    [Fact]
    public void Assignment_ReturnAsset_ThrowsWhenAssetNotInAssignment()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-TEST-4", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(Guid.NewGuid(), "ok");

        Assert.Throws<DomainException>(() => assignment.ReturnAsset(Guid.NewGuid(), ReturnResolution.Returned, DateTimeOffset.UtcNow, null, null, "tester", null));
    }

    [Fact]
    public void Assignment_ReturnAsset_ThrowsWhenAssignmentCancelled()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-TEST-5", DateTimeOffset.UtcNow, null, null, "tester");
        var assetId = Guid.NewGuid();
        assignment.AddAsset(assetId, "ok");
        typeof(Assignment).GetProperty(nameof(Assignment.Status))!.SetValue(assignment, AssignmentStatus.Cancelled);

        Assert.Throws<DomainException>(() => assignment.ReturnAsset(assetId, ReturnResolution.Returned, DateTimeOffset.UtcNow, null, null, "tester", null));
    }

    [Fact]
    public void Assignment_ReturnAsset_SetsPartiallyReturned_WhenSomeAssetsStillOpen()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-TEST-6", DateTimeOffset.UtcNow, null, null, "tester");
        var assetId1 = Guid.NewGuid();
        var assetId2 = Guid.NewGuid();
        assignment.AddAsset(assetId1, "ok");
        assignment.AddAsset(assetId2, "ok");

        assignment.ReturnAsset(assetId1, ReturnResolution.Returned, DateTimeOffset.UtcNow, "ok", null, "tester", null);

        Assert.Equal(AssignmentStatus.PartiallyReturned, assignment.Status);
    }

    [Fact]
    public void Assignment_ReturnAsset_SetsReturned_WhenAllAssetsResolved()
    {
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), "TEN-TEST-7", DateTimeOffset.UtcNow, null, null, "tester");
        var assetId1 = Guid.NewGuid();
        var assetId2 = Guid.NewGuid();
        assignment.AddAsset(assetId1, "ok");
        assignment.AddAsset(assetId2, "ok");

        assignment.ReturnAsset(assetId1, ReturnResolution.Returned, DateTimeOffset.UtcNow, "ok", null, "tester", null);
        assignment.ReturnAsset(assetId2, ReturnResolution.Missing, DateTimeOffset.UtcNow, null, null, "tester", null);

        Assert.Equal(AssignmentStatus.Returned, assignment.Status);
    }

    [Fact]
    public void Asset_ReleaseAssignment_ClearsOwnerAndSetsStatus()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "AT-1");
        asset.AssignTo(Guid.NewGuid());

        asset.ReleaseAssignment(AssetStatus.Damaged, "Magazyn A");

        Assert.Null(asset.AssignedPersonId);
        Assert.Equal(AssetStatus.Damaged, asset.Status);
        Assert.Equal("Magazyn A", asset.Location);
    }

    [Fact]
    public void Asset_ReleaseAssignment_ThrowsWhenAssetAlreadyDisposed()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "AT-1");
        asset.ChangeStatus(AssetStatus.Disposed);

        Assert.Throws<DomainException>(() => asset.ReleaseAssignment(AssetStatus.InStock));
    }

    [Fact]
    public async Task ReturnAssetAsync_ReturnsAssetToStock_AndClearsAssignedPerson()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        await service.AcceptAsync(created.Value!.Id, CancellationToken.None);

        var result = await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", "Magazyn", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssignmentStatus.Returned, result.Value!.Status);
        Assert.Equal(AssetStatus.InStock, asset.Status);
        Assert.Null(asset.AssignedPersonId);
        Assert.Equal("Magazyn", asset.Location);
    }

    [Fact]
    public async Task ReturnAssetAsync_Damaged_SetsAssetDamagedAndClearsAssignedPerson()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Damaged, "zniszczone", null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.Damaged, asset.Status);
        Assert.Null(asset.AssignedPersonId);
    }

    [Fact]
    public async Task ReturnAssetAsync_Missing_SetsAssetLost()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Missing, null, null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.Lost, asset.Status);
        Assert.Null(asset.AssignedPersonId);
    }

    [Fact]
    public async Task ReturnAssetAsync_Retained_KeepsAssignedPersonId_SetsRetired()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Retained, null, null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.Retired, asset.Status);
        Assert.Equal(person.Id, asset.AssignedPersonId);
    }

    [Fact]
    public async Task ReturnAssetAsync_WrittenOff_SetsAssetDisposed()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.WrittenOff, null, null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.Disposed, asset.Status);
        Assert.Null(asset.AssignedPersonId);
    }

    [Fact]
    public async Task ReturnAssetAsync_IsIdempotent_SecondCallDoesNotDuplicateActivityLog()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", null, null), CancellationToken.None);
        var secondResult = await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Damaged, "zniszczone", null, null), CancellationToken.None);

        Assert.True(secondResult.IsSuccess);
        Assert.Equal(ReturnResolution.Returned, secondResult.Value!.Assets.Single().ReturnResolution);
        Assert.Equal(AssetStatus.InStock, asset.Status);
    }

    [Fact]
    public async Task ReturnAssetAsync_TwoAssetAssignment_BecomesPartiallyReturned_ThenReturned()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset1 = AddAsset(user, assets);
        var asset2 = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset1.Id, "ok"), new AssignmentAssetRequest(asset2.Id, "ok")], [], null, null), CancellationToken.None);

        var afterFirst = await service.ReturnAssetAsync(created.Value!.Id, asset1.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", null, null), CancellationToken.None);
        Assert.Equal(AssignmentStatus.PartiallyReturned, afterFirst.Value!.Status);

        var afterSecond = await service.ReturnAssetAsync(created.Value!.Id, asset2.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", null, null), CancellationToken.None);
        Assert.Equal(AssignmentStatus.Returned, afterSecond.Value!.Status);
    }

    [Fact]
    public async Task ReturnAssetAsync_RejectsAssetNotBelongingToAssignment()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);
        var otherAsset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        var result = await service.ReturnAssetAsync(created.Value!.Id, otherAsset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ASSIGNMENT_ASSET_NOT_IN_ASSIGNMENT", result.Error!.Code);
    }

    [Fact]
    public async Task ReturnAsync_FullReturn_StillWorks_AndSkipsAlreadyResolvedItems()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset1 = AddAsset(user, assets);
        var asset2 = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset1.Id, "ok"), new AssignmentAssetRequest(asset2.Id, "ok")], [], null, null), CancellationToken.None);
        await service.ReturnAssetAsync(created.Value!.Id, asset1.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Damaged, "zniszczone", null, null), CancellationToken.None);

        var result = await service.ReturnAsync(created.Value!.Id, new ReturnAssignmentRequest("ok", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssignmentStatus.Returned, result.Value!.Status);
        var firstItem = result.Value!.Assets.Single(x => x.AssetId == asset1.Id);
        Assert.Equal(ReturnResolution.Damaged, firstItem.ReturnResolution);
        Assert.Equal(AssetStatus.Damaged, asset1.Status);
        var secondItem = result.Value!.Assets.Single(x => x.AssetId == asset2.Id);
        Assert.Equal(ReturnResolution.Returned, secondItem.ReturnResolution);
        Assert.Equal(AssetStatus.InStock, asset2.Status);
    }

    [Fact]
    public void Asset_MarkPendingReturn_SetsStatus_KeepsAssignedPersonId()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "AT-1");
        var personId = Guid.NewGuid();
        asset.AssignTo(personId);

        asset.MarkPendingReturn();

        Assert.Equal(AssetStatus.PendingReturn, asset.Status);
        Assert.Equal(personId, asset.AssignedPersonId);
    }

    [Fact]
    public void Asset_MarkPendingReturn_IsIdempotent()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "AT-1");
        asset.AssignTo(Guid.NewGuid());
        asset.MarkPendingReturn();

        asset.MarkPendingReturn();

        Assert.Equal(AssetStatus.PendingReturn, asset.Status);
    }

    [Fact]
    public void Asset_MarkPendingReturn_ThrowsWhenNotAssigned()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "AT-1");

        Assert.Throws<DomainException>(() => asset.MarkPendingReturn());
    }

    [Fact]
    public void Asset_AssignTo_RejectsPendingReturnAsset()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "AT-1");
        asset.AssignTo(Guid.NewGuid());
        asset.MarkPendingReturn();

        Assert.Throws<DomainException>(() => asset.AssignTo(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReturnAssetAsync_Returned_NoCategoryFound_DefaultsToInStock()
    {
        var (service, user, assets, people, _, _, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.InStock, asset.Status);
    }

    [Fact]
    public async Task ReturnAssetAsync_Returned_InspectionRequiredCategory_SetsInServiceAndCreatesPendingInspection()
    {
        var (service, user, assets, people, _, categories, inspections, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var category = new AssetCategory(user.OrganizationId, "Laptopy", AssetCategoryType.Physical, null);
        category.UpdateReturnPolicy(ReturnHandlingMode.InspectionRequired, PostReturnDisposition.Reuse, null, PhotoRequirement.Disabled, PhotoRequirement.Disabled);
        categories.Add(category);
        var asset = new Asset(user.OrganizationId, category.Id, "Laptop", "AT-CAT-1");
        assets.Add(asset);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.InService, asset.Status);
        var pendingInspection = Assert.Single(inspections.Inspections);
        Assert.Equal(asset.Id, pendingInspection.AssetId);
        Assert.False(pendingInspection.IsCompleted);
    }

    [Fact]
    public async Task ReturnAssetAsync_Returned_ReturnToVendorCategory_SetsInTransit()
    {
        var (service, user, assets, people, _, categories, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var category = new AssetCategory(user.OrganizationId, "Sprzęt wynajęty", AssetCategoryType.Physical, null);
        category.UpdateReturnPolicy(ReturnHandlingMode.DirectToStock, PostReturnDisposition.ReturnToVendor, null, PhotoRequirement.Disabled, PhotoRequirement.Disabled);
        categories.Add(category);
        var asset = new Asset(user.OrganizationId, category.Id, "Router", "AT-CAT-2");
        assets.Add(asset);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.InTransit, asset.Status);
    }

    [Fact]
    public async Task ReturnAssetAsync_Returned_DisposeCategory_SetsInService_NoInspectionCreated()
    {
        var (service, user, assets, people, _, categories, inspections, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var category = new AssetCategory(user.OrganizationId, "Materiały eksploatacyjne", AssetCategoryType.Consumable, null);
        category.UpdateReturnPolicy(ReturnHandlingMode.DirectToStock, PostReturnDisposition.Dispose, null, PhotoRequirement.Disabled, PhotoRequirement.Disabled);
        categories.Add(category);
        var asset = new Asset(user.OrganizationId, category.Id, "Toner", "AT-CAT-3");
        assets.Add(asset);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.InService, asset.Status);
        Assert.Empty(inspections.Inspections);
    }

    [Fact]
    public async Task ReturnAssetAsync_Returned_DirectToStockCategory_SetsInStock()
    {
        var (service, user, assets, people, _, categories, _, _, _, _) = CreateService();
        var person = AddPerson(user, people);
        var category = new AssetCategory(user.OrganizationId, "Myszy", AssetCategoryType.Physical, null);
        categories.Add(category);
        var asset = new Asset(user.OrganizationId, category.Id, "Mysz", "AT-CAT-4");
        assets.Add(asset);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        await service.ReturnAssetAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", null, null), CancellationToken.None);

        Assert.Equal(AssetStatus.InStock, asset.Status);
    }
}
