using Tenebit.Application.Abstractions;
using Tenebit.Application.Assignments;
using Tenebit.Application.Onboarding;
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
        var assignmentService = new AssignmentService(assignments, assets, categories, inspections, people, procedures, teams, organizations, activity, user, clock, unitOfWork, new FakePdfProtocolGenerator(), new FakeEmailSender(), new FakeAppLinkBuilder(), new InMemoryEquipmentReservationRepository());
        var service = new OnboardingService(teams, people, categories, assets, procedures, assignments, new EmptyJobProfileRepository(), activity, user, clock, unitOfWork, assignmentService);
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

    private sealed class EmptyJobProfileRepository : IJobProfileRepository
    {
        public Task<IReadOnlyList<JobProfile>> ListAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<JobProfile>>([]);
        public Task<JobProfile?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) => Task.FromResult<JobProfile?>(null);
        public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(JobProfile profile) { }
        public void Remove(JobProfile profile) { }
    }
}
