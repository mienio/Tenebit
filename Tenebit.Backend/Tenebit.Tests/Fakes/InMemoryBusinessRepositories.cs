using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Tests.Fakes;

public sealed class InMemoryAssetRepository : IAssetRepository
{
    public List<Asset> Assets { get; } = [];

    public Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, string? search, AssetStatus? status, string? location, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Asset>>(Assets.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<IReadOnlyList<Asset>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Asset>>(Assets.Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToList());

    public Task<Asset?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> AssetTagExistsAsync(Guid organizationId, string assetTag, Guid? excludingAssetId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public void Add(Asset asset) => Assets.Add(asset);
    public void Remove(Asset asset) => Assets.Remove(asset);
}

public sealed class InMemoryPersonRepository : IPersonRepository
{
    public List<Person> People { get; } = [];

    public Task<IReadOnlyList<Person>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Person>>(People.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<Person?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(People.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<Person?> FindByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        Task.FromResult(People.FirstOrDefault(x => x.OrganizationId == organizationId && x.Email == email));

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingPersonId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> HasBlockingRelationsAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public void Add(Person person) => People.Add(person);
    public void Remove(Person person) => People.Remove(person);
}

public sealed class InMemoryTeamRepository : ITeamRepository
{
    public List<Team> Teams { get; } = [];

    public Task<IReadOnlyList<Team>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Team>>(Teams.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<Team?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Teams.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingTeamId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public void Add(Team team) => Teams.Add(team);
}

public sealed class InMemoryProcedureRepository : IProcedureRepository
{
    public List<Procedure> Procedures { get; } = [];

    public Task<IReadOnlyList<Procedure>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Procedure>>(Procedures.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<IReadOnlyList<Procedure>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Procedure>>(Procedures.Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToList());

    public Task<Procedure?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Procedures.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<ProcedureDocument?> GetDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult(Procedures.FirstOrDefault(x => x.Id == procedureId)?.Documents.FirstOrDefault(x => x.Id == documentId));

    public void Add(Procedure procedure) => Procedures.Add(procedure);
    public void AddDocument(ProcedureDocument document) { }
    public void RemoveDocument(ProcedureDocument document) { }
}

public sealed class InMemoryAssignmentRepository : IAssignmentRepository
{
    public List<Assignment> Assignments { get; } = [];

    public Task<IReadOnlyList<Assignment>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Assignment>>(Assignments.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<Assignment?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public void Add(Assignment assignment) => Assignments.Add(assignment);
}

public sealed class InMemorySubscriptionRepository : ISubscriptionRepository
{
    public List<OrganizationSubscription> Subscriptions { get; } = [];

    public Task<OrganizationSubscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Subscriptions.FirstOrDefault(x => x.OrganizationId == organizationId));

    public void Add(OrganizationSubscription subscription) => Subscriptions.Add(subscription);
}

public sealed class FakePdfProtocolGenerator : IPdfProtocolGenerator
{
    public byte[] GenerateHandoverProtocol(ProtocolPdfModel model) => [1, 2, 3];
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid OrganizationId { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = "tester@acme.test";
    public string Language { get; set; } = "pl";
    public IReadOnlyCollection<string> Roles { get; set; } = ["owner"];
}
