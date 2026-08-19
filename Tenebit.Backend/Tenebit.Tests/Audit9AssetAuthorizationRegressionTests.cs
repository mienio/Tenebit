using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Application.Evidence;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class Audit9AssetAuthorizationRegressionTests
{
    private sealed record Fixture(
        FakeCurrentUser User,
        InMemoryAssetRepository Assets,
        InMemoryPersonRepository People,
        InMemoryTeamRepository Teams,
        AssetAuthorizationService Authorization,
        Asset AllowedAsset,
        Asset ForeignAsset);

    private static Fixture CreateManagerFixture()
    {
        var user = new FakeCurrentUser { Roles = [TenebitRoles.Manager] };
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var assets = new InMemoryAssetRepository();

        var manager = new Person(user.OrganizationId, "Marta", "Manager", "manager@example.test");
        var directReport = new Person(user.OrganizationId, "Anna", "Own", "own@example.test");
        directReport.Update("Anna", "Own", directReport.Email, null, null, "Pracownik", null, null, manager.Id, null, null);
        var foreignPerson = new Person(user.OrganizationId, "Beata", "Foreign", "foreign@example.test");
        people.Add(manager);
        people.Add(directReport);
        people.Add(foreignPerson);
        user.PersonId = manager.Id;

        var allowed = new Asset(user.OrganizationId, Guid.NewGuid(), "Allowed laptop", "AT-OWN");
        allowed.AssignTo(directReport.Id);
        assets.Add(allowed);
        var foreign = new Asset(user.OrganizationId, Guid.NewGuid(), "Foreign laptop", "AT-FOREIGN");
        foreign.AssignTo(foreignPerson.Id);
        assets.Add(foreign);

        var authorization = new AssetAuthorizationService(assets, new ManagerScopeService(people, teams), user);
        return new Fixture(user, assets, people, teams, authorization, allowed, foreign);
    }

    [Fact]
    public async Task Manager_CannotReadEvidenceOutsideManagedScope()
    {
        var f = CreateManagerFixture();
        var evidence = new InMemoryAssetEvidenceRepository();
        var assignments = new InMemoryAssignmentRepository();
        var item = new AssetEvidence(f.User.OrganizationId, f.ForeignAsset.Id, null, EvidencePhase.Issue,
            "photo.jpg", "image/jpeg", [1], new string('a', 64), null, "test", EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(item);
        var service = new AssetEvidenceService(evidence, f.Assets, assignments, new FakeImageSanitizer(), new InMemoryActivityLogRepository(), f.User, new FakeClock(), new FakeUnitOfWork(), f.Authorization);

        var list = await service.ListByAssetAsync(f.ForeignAsset.Id, CancellationToken.None);
        var single = await service.GetAsync(item.Id, CancellationToken.None);

        Assert.True(list.IsFailure);
        Assert.True(single.IsFailure);
        Assert.Equal(404, list.Error!.StatusCode);
        Assert.Equal(404, single.Error!.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotReadInspectionOutsideManagedScope()
    {
        var f = CreateManagerFixture();
        var inspections = new InMemoryAssetInspectionRepository();
        inspections.Add(new AssetInspection(f.User.OrganizationId, f.ForeignAsset.Id, null, DateTimeOffset.UtcNow, "test"));
        var service = new AssetInspectionService(inspections, f.Assets, new InMemoryActivityLogRepository(), f.User, new FakeClock(), new FakeUnitOfWork(), f.Authorization);

        var result = await service.GetPendingForAssetAsync(f.ForeignAsset.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotEnumerateOrReadTicketsOutsideManagedScope()
    {
        var f = CreateManagerFixture();
        var tickets = new InMemoryServiceTicketRepository();
        var inspections = new InMemoryAssetInspectionRepository();
        var own = new ServiceTicket(f.User.OrganizationId, f.AllowedAsset.Id, "OwnVendor", "own", null);
        var foreign = new ServiceTicket(f.User.OrganizationId, f.ForeignAsset.Id, "ForeignVendor", "foreign", null);
        tickets.Add(own);
        tickets.Add(foreign);
        tickets.AllowedScopedAssetIds.Add(f.AllowedAsset.Id);
        var service = new ServiceTicketService(tickets, f.Assets, inspections, new InMemoryActivityLogRepository(), f.User, new FakeClock(), new FakeUnitOfWork(), f.Authorization);

        var byForeignAsset = await service.ListByAssetAsync(f.ForeignAsset.Id, CancellationToken.None);
        var foreignById = await service.GetAsync(foreign.Id, CancellationToken.None);
        var page = await service.ListPagedAsync(null, 1, 100, CancellationToken.None);

        Assert.True(byForeignAsset.IsFailure);
        Assert.True(foreignById.IsFailure);
        Assert.True(page.IsSuccess);
        Assert.Single(page.Value!.Items);
        Assert.Equal(own.Id, page.Value.Items[0].Id);
    }

    [Fact]
    public async Task ServiceTicket_RejectsInspectionBelongingToDifferentAsset()
    {
        var user = new FakeCurrentUser { Roles = [TenebitRoles.AssetOperator] };
        var assets = new InMemoryAssetRepository();
        var assetA = new Asset(user.OrganizationId, Guid.NewGuid(), "A", "AT-A");
        var assetB = new Asset(user.OrganizationId, Guid.NewGuid(), "B", "AT-B");
        assets.Add(assetA);
        assets.Add(assetB);
        var inspections = new InMemoryAssetInspectionRepository();
        var inspectionB = new AssetInspection(user.OrganizationId, assetB.Id, null, DateTimeOffset.UtcNow, user.Subject);
        inspections.Add(inspectionB);
        var tickets = new InMemoryServiceTicketRepository();
        var auth = TestAuthorization.Asset(assets, user);
        var service = new ServiceTicketService(tickets, assets, inspections, new InMemoryActivityLogRepository(), user, new FakeClock(), new FakeUnitOfWork(), auth);

        var result = await service.OpenAsync(new OpenServiceTicketRequest(assetA.Id, inspectionB.Id, "Vendor", "desc", null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(tickets.Tickets);
    }
}
