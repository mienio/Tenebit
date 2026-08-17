using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Evidence;
using Tenebit.Application.Onboarding;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.People;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class OnboardingServiceTests
{
    private static (OnboardingService Service, FakeCurrentUser User, InMemoryPersonRepository People) CreateService()
    {
        var user = new FakeCurrentUser();
        var teams = new InMemoryTeamRepository();
        var people = new InMemoryPersonRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var inspections = new InMemoryAssetInspectionRepository();
        var assets = new InMemoryAssetRepository();
        var procedures = new InMemoryProcedureRepository();
        var assignments = new InMemoryAssignmentRepository();
        var activity = new InMemoryActivityLogRepository();
        var organizations = new InMemoryOrganizationRepository();
        var clock = new FakeClock();
        var unitOfWork = new FakeUnitOfWork();
        var evidence = new InMemoryAssetEvidenceRepository();
        var evidenceService = new AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, user, clock, unitOfWork);
        var assignmentService = new AssignmentService(assignments, assets, categories, inspections, people, procedures, teams, organizations, activity, user, clock, unitOfWork, new FakePdfProtocolGenerator(), new FakeEmailSender(), new FakeAppLinkBuilder(), new InMemoryEquipmentReservationRepository(), evidence, evidenceService,
            new AssetReturnDispositionService(inspections),
            new AssignmentResponseBuilder(assignments, people, assets, procedures, evidence, organizations),
            new AssignmentProtocolModelBuilder(assignments, people, teams, organizations, assets, procedures),
            new Tenebit.Application.Common.ManagerScopeService(people, teams));
        var service = new OnboardingService(teams, people, categories, assets, procedures, assignments, new EmptyJobProfileRepository(), activity, user, clock, unitOfWork, assignmentService, new Tenebit.Application.Common.ManagerScopeService(people, teams));
        return (service, user, people);
    }

    [Theory]
    [InlineData(EmploymentStatus.Offboarding)]
    [InlineData(EmploymentStatus.Inactive)]
    public async Task CreateEmployeePackageAsync_RejectsPersonWhoIsNotActive(EmploymentStatus status)
    {
        var (service, user, people) = CreateService();
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        if (status == EmploymentStatus.Offboarding) person.StartOffboarding(DateTimeOffset.UtcNow.AddDays(7));
        else person.Deactivate(DateTimeOffset.UtcNow);
        people.Add(person);

        var result = await service.CreateEmployeePackageAsync(new CreateEmployeePackageRequest(person.Id, null, [], [], null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("aktywnej osoby", result.Error!.Message);
    }

    [Fact]
    public async Task CreateEmployeePackageAsync_DoesNotResolvePersonFromAnotherOrganization()
    {
        var (service, _, people) = CreateService();
        var person = new Person(Guid.NewGuid(), "Jan", "Kowalski", "jan@other.test");
        people.Add(person);

        var result = await service.CreateEmployeePackageAsync(new CreateEmployeePackageRequest(person.Id, null, [], [], null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.DoesNotContain("aktywnej osoby", result.Error!.Message);
    }

    [Fact]
    public async Task CreateStarterPackageAsync_CreatesActivePerson()
    {
        var (service, _, people) = CreateService();
        var request = new CreateStarterPackageRequest("IT", "Jan", "Kowalski", "jan@acme.test", "Developer", "Laptop", "AT-001", null, "Laptopy", "Biuro", "Polityka sprzętowa", null, null);

        var result = await service.CreateStarterPackageAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var person = Assert.Single(people.People);
        Assert.Equal(EmploymentStatus.Active, person.EmploymentStatus);
        Assert.True(person.CanReceiveNewObligations);
    }

    [Fact]
    public async Task GetChecklistAsync_EmployeeCannotReadAnotherPersonsChecklist()
    {
        var (service, user, people) = CreateService();
        var self = new Person(user.OrganizationId, "Anna", "Pracownik", user.Email);
        people.Add(self);
        var other = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(other);

        user.Roles = ["employee"];

        var result = await service.GetChecklistAsync(other.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetChecklistAsync_EmployeeCanReadOwnChecklist()
    {
        var (service, user, people) = CreateService();
        var self = new Person(user.OrganizationId, "Anna", "Pracownik", user.Email);
        people.Add(self);

        user.Roles = ["employee"];

        var result = await service.GetChecklistAsync(self.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private static byte[] JpegBytes(int size = 32)
    {
        var bytes = new byte[Math.Max(size, 3)];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static (
        OnboardingService Service,
        FakeCurrentUser User,
        InMemoryAssetRepository Assets,
        InMemoryPersonRepository People,
        InMemoryAssignmentRepository Assignments,
        InMemoryAssetEvidenceRepository Evidence) CreateServiceWithEvidence()
    {
        var user = new FakeCurrentUser();
        var teams = new InMemoryTeamRepository();
        var people = new InMemoryPersonRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var inspections = new InMemoryAssetInspectionRepository();
        var assets = new InMemoryAssetRepository();
        var procedures = new InMemoryProcedureRepository();
        var assignments = new InMemoryAssignmentRepository();
        var activity = new InMemoryActivityLogRepository();
        var organizations = new InMemoryOrganizationRepository();
        var clock = new FakeClock();
        var unitOfWork = new FakeUnitOfWork();
        var evidence = new InMemoryAssetEvidenceRepository();
        var evidenceService = new AssetEvidenceService(evidence, assets, assignments, new FakeImageSanitizer(), activity, user, clock, unitOfWork);
        var assignmentService = new AssignmentService(assignments, assets, categories, inspections, people, procedures, teams, organizations, activity, user, clock, unitOfWork, new FakePdfProtocolGenerator(), new FakeEmailSender(), new FakeAppLinkBuilder(), new InMemoryEquipmentReservationRepository(), evidence, evidenceService,
            new AssetReturnDispositionService(inspections),
            new AssignmentResponseBuilder(assignments, people, assets, procedures, evidence, organizations),
            new AssignmentProtocolModelBuilder(assignments, people, teams, organizations, assets, procedures),
            new Tenebit.Application.Common.ManagerScopeService(people, teams));
        var service = new OnboardingService(teams, people, categories, assets, procedures, assignments, new EmptyJobProfileRepository(), activity, user, clock, unitOfWork, assignmentService, new Tenebit.Application.Common.ManagerScopeService(people, teams));
        return (service, user, assets, people, assignments, evidence);
    }

    [Fact]
    public async Task CreateEmployeePackageWithEvidenceAsync_CreatesAssignmentAndIssueEvidence()
    {
        var (service, user, assets, people, assignments, evidence) = CreateServiceWithEvidence();
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(person);
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-001");
        assets.Add(asset);

        var request = new CreateEmployeePackageRequest(person.Id, null, [asset.Id], [], null, null);
        var manifest = new Dictionary<string, EvidenceManifestEntry> { ["photo"] = new(asset.Id, null) };
        var files = new List<EvidenceFileInput> { new("photo", "photo.jpg", "image/jpeg", JpegBytes()) };

        var result = await service.CreateEmployeePackageWithEvidenceAsync(request, manifest, files, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(assignments.Assignments);
        Assert.Single(evidence.Items);
        Assert.Equal(EvidencePhase.Issue, evidence.Items[0].Phase);
        Assert.Equal(asset.Id, evidence.Items[0].AssetId);
    }

    [Fact]
    public async Task CreateEmployeePackageWithEvidenceAsync_InvalidFile_RollsBackAssignmentAndEvidence()
    {
        var (service, user, assets, people, assignments, evidence) = CreateServiceWithEvidence();
        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan@acme.test");
        people.Add(person);
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-001");
        assets.Add(asset);

        var request = new CreateEmployeePackageRequest(person.Id, null, [asset.Id], [], null, null);
        var manifest = new Dictionary<string, EvidenceManifestEntry> { ["photo"] = new(asset.Id, null) };
        var files = new List<EvidenceFileInput> { new("photo", "doc.pdf", "application/pdf", JpegBytes()) };

        var result = await service.CreateEmployeePackageWithEvidenceAsync(request, manifest, files, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(assignments.Assignments);
        Assert.Empty(evidence.Items);
    }

    private sealed class EmptyJobProfileRepository : IJobProfileRepository
    {
        public Task<IReadOnlyList<JobProfile>> ListAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<JobProfile>>([]);
        public Task<JobProfile?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) => Task.FromResult<JobProfile?>(null);
        public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(JobProfile profile) { }
        public void Remove(JobProfile profile) { }
    }
}
