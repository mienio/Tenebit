using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Evidence;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssignmentWithEvidenceTests
{
    private static byte[] JpegBytes(int size = 32)
    {
        var bytes = new byte[Math.Max(size, 3)];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static string Sha(string seed) => seed.PadLeft(64, '0');

    private static (
        AssignmentService Service,
        FakeCurrentUser User,
        InMemoryAssetRepository Assets,
        InMemoryPersonRepository People,
        InMemoryAssignmentRepository Assignments,
        InMemoryAssetEvidenceRepository Evidence) CreateService()
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
            assignments, assets, categories, inspections, people, procedures, teams, organizations,
            activity, currentUser, clock, unitOfWork, new FakePdfProtocolGenerator(), new FakeEmailSender(),
            new FakeAppLinkBuilder(), reservations, evidence, evidenceService, disposition, responseBuilder, protocolModelBuilder);

        return (service, currentUser, assets, people, assignments, evidence);
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

    // ---------- 6.6 Integralność: v1 vs v2 ----------

    [Fact]
    public void Assignment_IntegrityVersion1_IgnoresEvidence()
    {
        var orgId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var assignment = new Assignment(orgId, Guid.NewGuid(), "TEN-V1", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(assetId, "ok");
        var evidence = new AssetEvidence(orgId, assetId, assignment.Id, EvidencePhase.Issue, "a.jpg", "image/jpeg", JpegBytes(), Sha("a"), null, "tester", EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);

        assignment.Accept(DateTimeOffset.UtcNow, "1.2.3.4", new[] { evidence });

        Assert.Equal(1, assignment.IntegrityVersion);
        Assert.True(assignment.VerifyIntegrity());
        // Wersja 1 pomija dowody — inny zestaw zdjęć nie zmienia wyniku weryfikacji.
        Assert.True(assignment.VerifyIntegrity(Array.Empty<AssetEvidence>()));
    }

    [Fact]
    public void Assignment_IntegrityVersion2_IncludesOrderedIssueEvidence()
    {
        var orgId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var assignment = new Assignment(orgId, Guid.NewGuid(), "TEN-V2", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(assetId, "ok");
        assignment.EnableEvidenceIntegrity();

        var evidence = new AssetEvidence(orgId, assetId, assignment.Id, EvidencePhase.Issue, "a.jpg", "image/jpeg", JpegBytes(), Sha("a"), null, "tester", EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);

        assignment.Accept(DateTimeOffset.UtcNow, "1.2.3.4", new[] { evidence });

        Assert.Equal(2, assignment.IntegrityVersion);
        Assert.True(assignment.VerifyIntegrity(new[] { evidence }));
        // Brak dowodów przy wersji 2 zmienia hash.
        Assert.False(assignment.VerifyIntegrity(Array.Empty<AssetEvidence>()));
    }

    [Fact]
    public void Assignment_IntegrityVersion2_DetectsEvidenceTampering()
    {
        var orgId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var assignment = new Assignment(orgId, Guid.NewGuid(), "TEN-V2B", DateTimeOffset.UtcNow, null, null, "tester");
        assignment.AddAsset(assetId, "ok");
        assignment.EnableEvidenceIntegrity();

        var evidence = new AssetEvidence(orgId, assetId, assignment.Id, EvidencePhase.Issue, "a.jpg", "image/jpeg", JpegBytes(), Sha("a"), null, "tester", EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        assignment.Accept(DateTimeOffset.UtcNow, "1.2.3.4", new[] { evidence });
        Assert.True(assignment.VerifyIntegrity(new[] { evidence }));

        typeof(AssetEvidence).GetProperty(nameof(AssetEvidence.Sha256))!.SetValue(evidence, Sha("b"));

        Assert.False(assignment.VerifyIntegrity(new[] { evidence }));
    }

    // ---------- 6.4 Wydanie ze zdjęciami (transakcja) ----------

    [Fact]
    public async Task CreateWithEvidenceAsync_CreatesAssignmentAndIssueEvidence()
    {
        var (service, user, assets, people, assignments, evidence) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var request = new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null);
        var manifest = new Dictionary<string, EvidenceManifestEntry> { ["photo"] = new(asset.Id, "widok z przodu") };
        var files = new List<EvidenceFileInput> { new("photo", "photo.jpg", "image/jpeg", JpegBytes()) };

        var result = await service.CreateWithEvidenceAsync(request, manifest, files, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(assignments.Assignments);
        Assert.Equal(2, assignments.Assignments[0].IntegrityVersion);
        Assert.Single(evidence.Items);
        Assert.Equal(EvidencePhase.Issue, evidence.Items[0].Phase);
        Assert.Equal(asset.Id, evidence.Items[0].AssetId);
        Assert.Equal("widok z przodu", evidence.Items[0].Caption);
    }

    [Fact]
    public async Task CreateWithEvidenceAsync_InvalidFile_RollsBackAssignmentAndEvidence()
    {
        var (service, user, assets, people, assignments, evidence) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var request = new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null);
        var manifest = new Dictionary<string, EvidenceManifestEntry> { ["photo"] = new(asset.Id, null) };
        var files = new List<EvidenceFileInput> { new("photo", "doc.pdf", "application/pdf", JpegBytes()) };

        var result = await service.CreateWithEvidenceAsync(request, manifest, files, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(assignments.Assignments);
        Assert.Empty(evidence.Items);
    }

    // ---------- 6.5 Zwrot ze zdjęciami (transakcja) ----------

    [Fact]
    public async Task ReturnAssetWithEvidenceAsync_ReturnsAssetAndStoresReturnEvidence()
    {
        var (service, user, assets, people, assignments, evidence) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        Assert.True(created.IsSuccess);

        var files = new List<EvidenceFileInput> { new("photo", "return.jpg", "image/jpeg", JpegBytes()) };
        var result = await service.ReturnAssetWithEvidenceAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", "Magazyn", null), files, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssignmentStatus.Returned, result.Value!.Status);
        Assert.Equal(AssetStatus.InStock, asset.Status);
        Assert.Single(evidence.Items);
        Assert.Equal(EvidencePhase.Return, evidence.Items[0].Phase);
    }

    [Fact]
    public async Task ReturnAssetWithEvidenceAsync_InvalidFile_DoesNotMarkAssetReturned()
    {
        var (service, user, assets, people, assignments, evidence) = CreateService();
        var person = AddPerson(user, people);
        var asset = AddAsset(user, assets);

        var created = await service.CreateAsync(new CreateAssignmentRequest(person.Id, [new AssignmentAssetRequest(asset.Id, "ok")], [], null, null), CancellationToken.None);
        Assert.True(created.IsSuccess);

        var files = new List<EvidenceFileInput> { new("photo", "doc.pdf", "application/pdf", JpegBytes()) };
        var result = await service.ReturnAssetWithEvidenceAsync(created.Value!.Id, asset.Id, new ReturnAssignmentAssetItemRequest(ReturnResolution.Returned, "ok", "Magazyn", null), files, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssetStatus.Assigned, asset.Status);
        Assert.Empty(evidence.Items);
        Assert.Single(assignments.Assignments);
        Assert.Equal(AssignmentStatus.AwaitingAcceptance, assignments.Assignments[0].Status);
    }

    // ---------- 6.8 Publiczny odczyt dowodu (izolacja assignment) ----------

    [Fact]
    public async Task GetPublicAssignmentEvidenceAsync_ScopesEvidenceToSpecificAssignment()
    {
        var orgId = Guid.NewGuid();
        var assignments = new InMemoryAssignmentRepository();
        var evidence = new InMemoryAssetEvidenceRepository();
        var assets = new InMemoryAssetRepository();
        var activity = new InMemoryActivityLogRepository();

        var assignmentA = new Assignment(orgId, Guid.NewGuid(), "TEN-A", DateTimeOffset.UtcNow, null, null, "system");
        var assignmentB = new Assignment(orgId, Guid.NewGuid(), "TEN-B", DateTimeOffset.UtcNow, null, null, "system");
        assignments.Add(assignmentA);
        assignments.Add(assignmentB);

        var item = new AssetEvidence(orgId, Guid.NewGuid(), assignmentA.Id, EvidencePhase.Issue, "a.jpg", "image/jpeg", JpegBytes(), Sha("a"), null, "tester", EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(item);

        var service = new AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, new FakeCurrentUser(), new FakeClock(), new FakeUnitOfWork());

        Assert.True((await service.GetPublicAssignmentEvidenceAsync(orgId, assignmentA.Id, item.Id, CancellationToken.None)).IsSuccess);

        var wrongAssignment = await service.GetPublicAssignmentEvidenceAsync(orgId, assignmentB.Id, item.Id, CancellationToken.None);
        Assert.True(wrongAssignment.IsFailure);
        Assert.Equal("EVIDENCE_NOT_FOUND", wrongAssignment.Error!.Code);

        var wrongOrganization = await service.GetPublicAssignmentEvidenceAsync(Guid.NewGuid(), assignmentA.Id, item.Id, CancellationToken.None);
        Assert.True(wrongOrganization.IsFailure);
        Assert.Equal("ASSIGNMENT_NOT_FOUND", wrongOrganization.Error!.Code);
    }

    [Fact]
    public async Task GetPublicAssignmentEvidenceAsync_RejectsReturnPhaseEvidence()
    {
        var orgId = Guid.NewGuid();
        var assignments = new InMemoryAssignmentRepository();
        var evidence = new InMemoryAssetEvidenceRepository();
        var assets = new InMemoryAssetRepository();
        var activity = new InMemoryActivityLogRepository();

        var assignment = new Assignment(orgId, Guid.NewGuid(), "TEN-R", DateTimeOffset.UtcNow, null, null, "system");
        assignments.Add(assignment);

        var returnItem = new AssetEvidence(orgId, Guid.NewGuid(), assignment.Id, EvidencePhase.Return, "return.jpg", "image/jpeg", JpegBytes(), Sha("r"), null, "tester", EvidenceUploadSource.AuthenticatedUser, DateTimeOffset.UtcNow);
        evidence.Add(returnItem);

        var service = new AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, new FakeCurrentUser(), new FakeClock(), new FakeUnitOfWork());

        var result = await service.GetPublicAssignmentEvidenceAsync(orgId, assignment.Id, returnItem.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EVIDENCE_NOT_FOUND", result.Error!.Code);
    }
}
