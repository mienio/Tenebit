using Tenebit.Application.Assignments;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssignmentServiceTests
{
    private static (AssignmentService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemoryPersonRepository People, InMemoryProcedureRepository Procedures) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var people = new InMemoryPersonRepository();
        var procedures = new InMemoryProcedureRepository();
        var teams = new InMemoryTeamRepository();
        var organizations = new InMemoryOrganizationRepository();
        var activity = new InMemoryActivityLogRepository();

        var service = new AssignmentService(
            new InMemoryAssignmentRepository(),
            assets,
            people,
            procedures,
            teams,
            organizations,
            activity,
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork(),
            new FakePdfProtocolGenerator(),
            new FakeEmailSender(),
            new FakeAppLinkBuilder());

        return (service, currentUser, assets, people, procedures);
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
        var (service, user, _, people, _) = CreateService();
        var person = AddPerson(user, people);

        var result = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [], [], null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsAssetThatIsAlreadyAssigned()
    {
        var (service, user, assets, people, _) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);
        asset.AssignTo(Guid.NewGuid());

        var result = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_OnlyRequiresAcceptanceForPublishedProcedures()
    {
        var (service, user, assets, people, procedures) = CreateService();
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
        var (service, user, assets, people, _) = CreateService();
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
        var (service, user, assets, people, _) = CreateService();
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
        var (service, user, assets, people, _) = CreateService();
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
}
