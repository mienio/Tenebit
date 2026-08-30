using System.Security.Cryptography;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Dashboards;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Tests.Fakes;

public sealed class InMemoryAssetRepository : IAssetRepository
{
    public List<Asset> Assets { get; } = [];
    public List<Assignment> Assignments { get; } = [];
    public List<AssetAuditItem> AssetAuditItems { get; } = [];
    public List<Tenebit.Domain.Reservations.EquipmentReservationItem> ReservationItems { get; } = [];
    public List<OffboardingItem> OffboardingItems { get; } = [];
    public List<AssetInspection> AssetInspections { get; } = [];
    public List<ServiceTicket> ServiceTickets { get; } = [];

    public Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, string? search, AssetStatus? status, string? location, CancellationToken cancellationToken)
    {
        var prefix = string.IsNullOrWhiteSpace(location) ? null : location.Trim() + " / ";
        var normalized = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        IReadOnlyList<Asset> rows = Assets
            .Where(x => x.OrganizationId == organizationId
                && (!status.HasValue || x.Status == status.Value)
                && (normalized is null
                    || x.Location == normalized
                    || (x.Location != null && prefix != null && x.Location.StartsWith(prefix))))
            .ToList();
        return Task.FromResult<IReadOnlyList<Asset>>(rows);
    }

    public async Task<IReadOnlyList<Asset>> ListScopedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        var rows = await ListAsync(organizationId, search, status, location, cancellationToken);
        return rows.Where(x => (x.AssignedPersonId.HasValue && personIds.Contains(x.AssignedPersonId.Value)) || (x.TeamId.HasValue && teamIds.Contains(x.TeamId.Value))).ToList();
    }

    public Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken)
    {
        var prefix = string.IsNullOrWhiteSpace(location) ? null : location.Trim() + " / ";
        var normalized = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        var rows = Assets
            .Where(x => x.OrganizationId == organizationId
                && (normalized is null
                    || x.Location == normalized
                    || (x.Location != null && prefix != null && x.Location.StartsWith(prefix))))
            .ToList();
        return Task.FromResult<(IReadOnlyList<Asset>, int)>((rows, rows.Count));
    }

    public async Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedScopedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        var rows = (await ListScopedAsync(organizationId, search, status, location, personIds, teamIds, cancellationToken))
            .Where(x => !teamId.HasValue || x.TeamId == teamId.Value)
            .Where(x => !categoryId.HasValue || x.CategoryId == categoryId.Value)
            .Where(x => !unassignedOnly || x.AssignedPersonId is null)
            .Where(x => !warrantyFrom.HasValue || (x.WarrantyUntil.HasValue && x.WarrantyUntil.Value >= warrantyFrom.Value))
            .Where(x => !warrantyTo.HasValue || (x.WarrantyUntil.HasValue && x.WarrantyUntil.Value <= warrantyTo.Value))
            .ToList();
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        return (rows.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(), rows.Count);
    }

    public Task<(IReadOnlyDictionary<Guid, int> ByCategory, IReadOnlyDictionary<AssetStatus, int> ByStatus, IReadOnlyDictionary<Guid, int> ByPerson)> GetGroupCountsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = Assets.Where(x => x.OrganizationId == organizationId).ToList();
        IReadOnlyDictionary<Guid, int> byCategory = rows.GroupBy(x => x.CategoryId).ToDictionary(g => g.Key, g => g.Count());
        IReadOnlyDictionary<AssetStatus, int> byStatus = rows.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
        IReadOnlyDictionary<Guid, int> byPerson = rows.Where(x => x.AssignedPersonId.HasValue).GroupBy(x => x.AssignedPersonId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult((byCategory, byStatus, byPerson));
    }

    public async Task<(IReadOnlyDictionary<Guid, int> ByCategory, IReadOnlyDictionary<AssetStatus, int> ByStatus, IReadOnlyDictionary<Guid, int> ByPerson)> GetGroupCountsScopedAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        var rows = await ListScopedAsync(organizationId, null, null, null, personIds, teamIds, cancellationToken);
        IReadOnlyDictionary<Guid, int> byCategory = rows.GroupBy(x => x.CategoryId).ToDictionary(g => g.Key, g => g.Count());
        IReadOnlyDictionary<AssetStatus, int> byStatus = rows.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
        IReadOnlyDictionary<Guid, int> byPerson = rows.Where(x => x.AssignedPersonId.HasValue).GroupBy(x => x.AssignedPersonId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return (byCategory, byStatus, byPerson);
    }

    public Task<IReadOnlyList<Asset>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Asset>>(Assets.Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToList());

    public Task<IReadOnlyList<Asset>> ListByAssignedPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Asset>>(Assets.Where(x => x.OrganizationId == organizationId && x.AssignedPersonId == personId).ToList());

    public Task<IReadOnlyList<Asset>> ListWarrantyExpiringAsync(Guid organizationId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Asset>>(Assets.Where(x => x.OrganizationId == organizationId && x.WarrantyUntil.HasValue && x.WarrantyUntil.Value >= from && x.WarrantyUntil.Value <= to).ToList());

    public Task<Asset?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> AssetTagExistsAsync(Guid organizationId, string assetTag, Guid? excludingAssetId, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.Any(x => x.OrganizationId == organizationId && x.AssetTag == assetTag && (!excludingAssetId.HasValue || x.Id != excludingAssetId.Value)));

    public Task<Asset?> FindByScanCodeAsync(string scanCode, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.FirstOrDefault(x => x.ScanCode == scanCode));

    public Task<bool> ScanCodeExistsAsync(string scanCode, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.Any(x => x.ScanCode == scanCode));

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.Count(x => x.OrganizationId == organizationId));

    public Task<int> CountByLocationAsync(Guid organizationId, string location, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.Count(x => x.OrganizationId == organizationId && x.Location == location));

    public Task<int> CountByLocationIdAsync(Guid organizationId, Guid locationId, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.Count(x => x.OrganizationId == organizationId && x.LocationId == locationId));

    public Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(
            Assignments.Any(a => a.OrganizationId == organizationId && a.Assets.Any(x => x.AssetId == id))
            || AssetAuditItems.Any(x => x.OrganizationId == organizationId && x.AssetId == id)
            || ReservationItems.Any(x => x.OrganizationId == organizationId && (x.AssetId == id || x.OriginalAssetId == id))
            || OffboardingItems.Any(x => x.OrganizationId == organizationId && x.AssetId == id)
            || AssetInspections.Any(x => x.OrganizationId == organizationId && x.AssetId == id)
            || ServiceTickets.Any(x => x.OrganizationId == organizationId && x.AssetId == id));

    public void Add(Asset asset) => Assets.Add(asset);
    public void Remove(Asset asset) => Assets.Remove(asset);
}

public sealed class InMemoryPersonRepository : IPersonRepository
{
    public List<Person> People { get; } = [];

    public Task<IReadOnlyList<Person>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Person>>(People.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<IReadOnlyList<Person>> ListScopedAsync(Guid organizationId, string? search, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Person>>(People.Where(x => x.OrganizationId == organizationId && personIds.Contains(x.Id)).ToList());

    public Task<(IReadOnlyList<Person> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = People.Where(x => x.OrganizationId == organizationId).ToList();
        return Task.FromResult<(IReadOnlyList<Person>, int)>((rows, rows.Count));
    }

    public Task<(IReadOnlyList<Person> Items, int Total)> ListPagedScopedAsync(Guid organizationId, string? search, int page, int pageSize, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken)
    {
        var rows = People.Where(x => x.OrganizationId == organizationId && personIds.Contains(x.Id)).ToList();
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        return Task.FromResult<(IReadOnlyList<Person>, int)>((rows.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(), rows.Count));
    }

    public Task<IReadOnlyList<Guid>> ListManagedScopePersonIdsAsync(Guid organizationId, Guid managerPersonId, IReadOnlyCollection<Guid> managedTeamIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(People
            .Where(x => x.OrganizationId == organizationId && (x.Id == managerPersonId || x.ManagerId == managerPersonId || (x.TeamId.HasValue && managedTeamIds.Contains(x.TeamId.Value))))
            .Select(x => x.Id)
            .ToList());

    public Task<Person?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(People.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<Person?> FindByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        Task.FromResult(People.FirstOrDefault(x => x.OrganizationId == organizationId && x.Email == email));

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingPersonId, CancellationToken cancellationToken) =>
        Task.FromResult(People.Any(x => x.OrganizationId == organizationId && x.Email == email && (!excludingPersonId.HasValue || x.Id != excludingPersonId.Value)));

    public bool HasBlockingRelations { get; set; }

    public Task<bool> HasBlockingRelationsAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult(HasBlockingRelations);

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(People.Count(x => x.OrganizationId == organizationId));

    public Task<int> CountByLocationAsync(Guid organizationId, string location, CancellationToken cancellationToken) =>
        Task.FromResult(People.Count(x => x.OrganizationId == organizationId && x.Location == location));

    public Task<int> CountByLocationIdAsync(Guid organizationId, Guid locationId, CancellationToken cancellationToken) =>
        Task.FromResult(People.Count(x => x.OrganizationId == organizationId && x.LocationId == locationId));

    public void Add(Person person) => People.Add(person);
    public void Remove(Person person) => People.Remove(person);
}

public sealed class InMemoryTeamRepository : ITeamRepository
{
    public List<Team> Teams { get; } = [];
    public List<Person> People { get; } = [];
    public List<Asset> Assets { get; } = [];

    public Task<IReadOnlyList<Team>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Team>>(Teams.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<IReadOnlyList<Guid>> ListManagedIdsAsync(Guid organizationId, Guid managerPersonId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Teams.Where(x => x.OrganizationId == organizationId && x.ManagerId == managerPersonId).Select(x => x.Id).ToList());

    public Task<Team?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Teams.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingTeamId, CancellationToken cancellationToken) =>
        Task.FromResult(Teams.Any(x => x.OrganizationId == organizationId && x.Name == name && (!excludingTeamId.HasValue || x.Id != excludingTeamId.Value)));

    public Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(People.Any(x => x.OrganizationId == organizationId && x.TeamId == id) || Assets.Any(x => x.OrganizationId == organizationId && x.TeamId == id));

    public void Add(Team team) => Teams.Add(team);
    public void Remove(Team team) => Teams.Remove(team);
}

public sealed class InMemoryPersonRelationTypeRepository : IPersonRelationTypeRepository
{
    public List<PersonRelationType> RelationTypes { get; } = [];
    public List<Person> People { get; } = [];

    public Task<IReadOnlyList<PersonRelationType>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PersonRelationType>>(RelationTypes.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.SortOrder).ToList());

    public Task<PersonRelationType?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(RelationTypes.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(RelationTypes.Any(x => x.OrganizationId == organizationId && x.Name == name.Trim() && (!excludingId.HasValue || x.Id != excludingId.Value)));

    public Task<bool> IsUsedAsync(Guid organizationId, string name, CancellationToken cancellationToken) =>
        Task.FromResult(People.Any(x => x.OrganizationId == organizationId && x.RelationType == name));

    public void Add(PersonRelationType relationType) => RelationTypes.Add(relationType);
    public void Remove(PersonRelationType relationType) => RelationTypes.Remove(relationType);
}

public sealed class InMemoryProcedureRepository : IProcedureRepository
{
    public List<Procedure> Procedures { get; } = [];

    public Task<IReadOnlyList<Procedure>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Procedure>>(Procedures.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Procedures.Count(x => x.OrganizationId == organizationId));

    public Task<(IReadOnlyList<Procedure> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Procedures.Where(x => x.OrganizationId == organizationId).ToList();
        return Task.FromResult<(IReadOnlyList<Procedure>, int)>((rows, rows.Count));
    }

    public Task<IReadOnlyList<Procedure>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Procedure>>(Procedures.Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToList());

    public Task<Procedure?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Procedures.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<IReadOnlyList<ProcedureDocumentMetadata>> ListDocumentMetadataByProcedureIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> procedureIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProcedureDocumentMetadata>>(Procedures
            .Where(x => x.OrganizationId == organizationId && procedureIds.Contains(x.Id))
            .SelectMany(x => x.Documents)
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new ProcedureDocumentMetadata(x.Id, x.ProcedureId, x.FileName, x.ContentType, x.SizeBytes, x.UploadedAt, x.UploadedBy))
            .ToList());

    public Task<ProcedureDocumentMetadata?> GetDocumentMetadataAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = Procedures.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == procedureId)?.Documents.FirstOrDefault(x => x.Id == documentId);
        return Task.FromResult(document is null ? null : new ProcedureDocumentMetadata(document.Id, document.ProcedureId, document.FileName, document.ContentType, document.SizeBytes, document.UploadedAt, document.UploadedBy));
    }

    public Task<bool> HasDocumentsAsync(Guid organizationId, Guid procedureId, CancellationToken cancellationToken) =>
        Task.FromResult(Procedures.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == procedureId)?.Documents.Count > 0);

    public Task<ProcedureDocument?> GetDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult(Procedures.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == procedureId)?.Documents.FirstOrDefault(x => x.Id == documentId));

    public Task<bool> DeleteDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken)
    {
        var procedure = Procedures.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == procedureId);
        var document = procedure?.Documents.FirstOrDefault(x => x.Id == documentId);
        if (procedure is null || document is null) return Task.FromResult(false);
        procedure.Documents.Remove(document);
        return Task.FromResult(true);
    }

    public void Add(Procedure procedure) => Procedures.Add(procedure);
    public void AddDocument(ProcedureDocument document) { }
}

public sealed class InMemoryJobProfileRepository : IJobProfileRepository
{
    public List<JobProfile> Profiles { get; } = [];

    public Task<IReadOnlyList<JobProfile>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JobProfile>>(Profiles.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<JobProfile?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.Any(x => x.OrganizationId == organizationId && x.Name == name && (!excludingId.HasValue || x.Id != excludingId.Value)));

    public void Add(JobProfile profile) => Profiles.Add(profile);
    public void Remove(JobProfile profile) => Profiles.Remove(profile);
}

public sealed class InMemoryAssignmentRepository : IAssignmentRepository
{
    public List<Assignment> Assignments { get; } = [];

    public Task<IReadOnlyList<Assignment>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Assignment>>(Assignments.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<IReadOnlyList<Assignment>> ListByPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Assignment>>(Assignments.Where(x => x.OrganizationId == organizationId && x.PersonId == personId).ToList());

    public Task<IReadOnlyList<Assignment>> ListByPersonIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Assignment>>(Assignments.Where(x => x.OrganizationId == organizationId && personIds.Contains(x.PersonId)).ToList());

    public Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Assignments.Where(x => x.OrganizationId == organizationId).ToList();
        return Task.FromResult<(IReadOnlyList<Assignment>, int)>((rows, rows.Count));
    }

    public Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedByPersonIdsAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken)
    {
        var rows = Assignments.Where(x => x.OrganizationId == organizationId && personIds.Contains(x.PersonId) && (!status.HasValue || x.Status == status.Value)).ToList();
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        return Task.FromResult<(IReadOnlyList<Assignment>, int)>((rows.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(), rows.Count));
    }

    public Task<IReadOnlyList<Guid>> ListProcedureIdsByPersonIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Assignments.Where(x => x.OrganizationId == organizationId && personIds.Contains(x.PersonId))
            .SelectMany(x => x.ProcedureAcceptances).Select(x => x.ProcedureId).Distinct().ToList());

    public Task<bool> HasProcedureAssignmentAsync(Guid organizationId, Guid personId, Guid procedureId, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.Any(x => x.OrganizationId == organizationId && x.PersonId == personId && x.ProcedureAcceptances.Any(a => a.ProcedureId == procedureId)));

    public Task<bool> HasProcedureAssignmentForPeopleAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, Guid procedureId, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.Any(x => x.OrganizationId == organizationId && personIds.Contains(x.PersonId) && x.ProcedureAcceptances.Any(a => a.ProcedureId == procedureId)));

    public Task<Assignment?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<Assignment?> FindByPublicTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.FirstOrDefault(x => x.PublicTokenHash == tokenHash));

    public void Add(Assignment assignment) => Assignments.Add(assignment);
}

public sealed class InMemorySubscriptionRepository : ISubscriptionRepository
{
    public List<OrganizationSubscription> Subscriptions { get; } = [];

    public Task<OrganizationSubscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Subscriptions.FirstOrDefault(x => x.OrganizationId == organizationId));

    public Task<OrganizationSubscription?> GetByStripeCustomerAsync(string stripeCustomerId, CancellationToken cancellationToken) =>
        Task.FromResult(Subscriptions.FirstOrDefault(x => x.StripeCustomerId == stripeCustomerId));

    public Task<IReadOnlyList<OrganizationSubscription>> ListWithStripeSubscriptionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OrganizationSubscription>>(Subscriptions.Where(x => !string.IsNullOrWhiteSpace(x.StripeSubscriptionId)).ToList());

    public void Add(OrganizationSubscription subscription) => Subscriptions.Add(subscription);
}

public sealed class InMemoryProcessedStripeEventRepository : IProcessedStripeEventRepository
{
    public List<ProcessedStripeEvent> Events { get; } = [];

    public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken) =>
        Task.FromResult(Events.Any(x => x.EventId == eventId));

    public void Add(ProcessedStripeEvent processedEvent) => Events.Add(processedEvent);
}

public sealed class InMemoryDashboardLayoutRepository : IDashboardLayoutRepository
{
    public List<DashboardLayout> Layouts { get; } = [];

    public Task<DashboardLayout?> GetAsync(Guid organizationId, Guid organizationUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Layouts.FirstOrDefault(x => x.OrganizationId == organizationId && x.OrganizationUserId == organizationUserId));

    public void Add(DashboardLayout layout) => Layouts.Add(layout);
}

public sealed class InMemoryDashboardSnapshotRepository : IDashboardSnapshotRepository
{
    public List<DashboardSnapshot> Snapshots { get; } = [];

    public Task<DashboardSnapshot?> GetForDateAsync(Guid organizationId, DateOnly date, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshots.FirstOrDefault(x => x.OrganizationId == organizationId && x.SnapshotDate == date));

    public Task<DashboardSnapshot?> GetClosestOnOrBeforeAsync(Guid organizationId, DateOnly onOrBefore, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshots
            .Where(x => x.OrganizationId == organizationId && x.SnapshotDate <= onOrBefore)
            .OrderByDescending(x => x.SnapshotDate)
            .FirstOrDefault());

    public void Add(DashboardSnapshot snapshot) => Snapshots.Add(snapshot);
}

public sealed class InMemoryLicenseRepository : ILicenseRepository
{
    public List<License> Licenses { get; } = [];

    public Task<IReadOnlyList<License>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<License>>(Licenses.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<License?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Licenses.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Licenses.Count(x => x.OrganizationId == organizationId));

    public void Add(License license) => Licenses.Add(license);
    public void Remove(License license) => Licenses.Remove(license);
}

public sealed class InMemoryRolePermissionRepository : IRolePermissionRepository
{
    public List<RolePermission> Permissions { get; } = [];

    public Task<IReadOnlyList<RolePermission>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RolePermission>>(Permissions.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<RolePermission?> FindAsync(Guid organizationId, string roleKey, string permissionKey, CancellationToken cancellationToken) =>
        Task.FromResult(Permissions.FirstOrDefault(x => x.OrganizationId == organizationId && x.RoleKey == roleKey && x.PermissionKey == permissionKey));

    public void Add(RolePermission permission) => Permissions.Add(permission);
    public void Remove(RolePermission permission) => Permissions.Remove(permission);
}

public sealed class InMemoryOffboardingCaseRepository : IOffboardingCaseRepository
{
    private static readonly OffboardingCaseStatus[] ClosedStatuses = [OffboardingCaseStatus.Completed, OffboardingCaseStatus.Cancelled];

    public List<OffboardingCase> Cases { get; } = [];

    public Task<(IReadOnlyList<OffboardingCase> Items, int Total)> ListPagedAsync(Guid organizationId, OffboardingCaseStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Cases.Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status.Value)).ToList();
        return Task.FromResult<(IReadOnlyList<OffboardingCase>, int)>((rows, rows.Count));
    }

    public Task<IReadOnlyList<OffboardingCase>> ListOpenAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OffboardingCase>>(Cases.Where(x => x.OrganizationId == organizationId && (x.Status == OffboardingCaseStatus.Active || x.Status == OffboardingCaseStatus.WaitingForReturn)).ToList());

    public Task<OffboardingCase?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Cases.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<OffboardingCase?> FindOpenByPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult(Cases.FirstOrDefault(x => x.OrganizationId == organizationId && x.PersonId == personId && !ClosedStatuses.Contains(x.Status)));

    public Task<OffboardingCase?> FindByPublicTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Cases.FirstOrDefault(x => x.PublicTokenHash == tokenHash));

    public void Add(OffboardingCase offboardingCase) => Cases.Add(offboardingCase);
}

public sealed class InMemoryOffboardingItemRepository : IOffboardingItemRepository
{
    public List<OffboardingItem> Items { get; } = [];

    public Task<IReadOnlyList<OffboardingItem>> ListByCaseAsync(Guid organizationId, Guid offboardingCaseId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OffboardingItem>>(Items
            .Where(x => x.OrganizationId == organizationId && x.OffboardingCaseId == offboardingCaseId)
            .OrderBy(x => x.SortOrder)
            .ToList());

    public Task<OffboardingItem?> GetAsync(Guid organizationId, Guid offboardingCaseId, Guid itemId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.OrganizationId == organizationId && x.OffboardingCaseId == offboardingCaseId && x.Id == itemId));

    public void Add(OffboardingItem item) => Items.Add(item);
}

public sealed class InMemoryAssetAuditCampaignRepository : IAssetAuditCampaignRepository
{
    public List<AssetAuditCampaign> Campaigns { get; } = [];

    public Task<(IReadOnlyList<AssetAuditCampaign> Items, int Total)> ListPagedAsync(Guid organizationId, AssetAuditCampaignStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Campaigns.Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status.Value)).ToList();
        return Task.FromResult<(IReadOnlyList<AssetAuditCampaign>, int)>((rows, rows.Count));
    }

    public Task<IReadOnlyList<AssetAuditCampaign>> ListActiveAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetAuditCampaign>>(Campaigns.Where(x => x.OrganizationId == organizationId && x.Status == AssetAuditCampaignStatus.Active).ToList());

    public Task<AssetAuditCampaign?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Campaigns.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public void Add(AssetAuditCampaign campaign) => Campaigns.Add(campaign);
}

public sealed class InMemoryAssetAuditParticipantRepository : IAssetAuditParticipantRepository
{
    public List<AssetAuditParticipant> Participants { get; } = [];

    public Task<IReadOnlyList<AssetAuditParticipant>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetAuditParticipant>>(Participants.Where(x => x.OrganizationId == organizationId && x.CampaignId == campaignId).ToList());

    public Task<AssetAuditParticipant?> GetAsync(Guid organizationId, Guid campaignId, Guid participantId, CancellationToken cancellationToken) =>
        Task.FromResult(Participants.FirstOrDefault(x => x.OrganizationId == organizationId && x.CampaignId == campaignId && x.Id == participantId));

    public void Add(AssetAuditParticipant participant) => Participants.Add(participant);

    public Task<AssetAuditParticipant?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Participants.FirstOrDefault(x => x.TokenHash == tokenHash));
}

public sealed class InMemoryAssetAuditItemRepository : IAssetAuditItemRepository
{
    public List<AssetAuditItem> Items { get; } = [];

    public Task<IReadOnlyList<AssetAuditItem>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetAuditItem>>(Items.Where(x => x.OrganizationId == organizationId && x.CampaignId == campaignId).ToList());

    public Task<IReadOnlyList<AssetAuditItem>> ListByParticipantAsync(Guid organizationId, Guid participantId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetAuditItem>>(Items.Where(x => x.OrganizationId == organizationId && x.ParticipantId == participantId).ToList());

    public void Add(AssetAuditItem item) => Items.Add(item);
}

public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool IsConfigured { get; set; } = true;
    public bool AllPlansConfigured { get; set; } = true;
    public HashSet<string> ConfiguredPlanKeys { get; } = [];
    public string NextCustomerId { get; set; } = "cus_fake";
    public string NextCheckoutUrl { get; set; } = "https://checkout.stripe.com/fake-session";
    public string NextPortalUrl { get; set; } = "https://billing.stripe.com/fake-portal";
    public PaymentWebhookEvent? NextWebhookEvent { get; set; }
    public PaymentSubscriptionState? NextCanonicalSubscription { get; set; }
    public bool ThrowOnParseWebhookEvent { get; set; }

    public string? LastCustomerIdempotencyKey { get; private set; }
    public string? LastCheckoutIdempotencyKey { get; private set; }
    public string? LastCheckoutPlanKey { get; private set; }
    public int CheckoutCreateCalls { get; private set; }

    public bool IsPlanConfigured(string planKey) => AllPlansConfigured || ConfiguredPlanKeys.Contains(planKey);

    public Task<string> CreateCustomerAsync(string email, Guid organizationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        LastCustomerIdempotencyKey = idempotencyKey;
        return Task.FromResult(NextCustomerId);
    }

    public Task<string> CreateCheckoutSessionAsync(string customerId, Guid organizationId, string planKey, string successUrl, string cancelUrl, string idempotencyKey, CancellationToken cancellationToken)
    {
        LastCheckoutIdempotencyKey = idempotencyKey;
        LastCheckoutPlanKey = planKey;
        CheckoutCreateCalls++;
        return Task.FromResult(NextCheckoutUrl);
    }

    public Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken) =>
        Task.FromResult(NextPortalUrl);

    public PaymentWebhookEvent? ParseWebhookEvent(string payload, string signatureHeader)
    {
        if (ThrowOnParseWebhookEvent) throw new PaymentWebhookValidationException("Invalid Stripe webhook signature.");
        return NextWebhookEvent;
    }

    public Task<PaymentSubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken) =>
        Task.FromResult(NextCanonicalSubscription ?? (NextWebhookEvent is null ? null : new PaymentSubscriptionState(
            NextWebhookEvent.CustomerId,
            NextWebhookEvent.SubscriptionId ?? subscriptionId,
            NextWebhookEvent.PlanKey,
            NextWebhookEvent.Status,
            NextWebhookEvent.CurrentPeriodStart,
            NextWebhookEvent.CurrentPeriodEnd,
            NextWebhookEvent.OrganizationId)));
}

public sealed class InMemoryServiceTicketRepository : IServiceTicketRepository
{
    public List<ServiceTicket> Tickets { get; } = [];

    public Task<ServiceTicket?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Tickets.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<IReadOnlyList<ServiceTicket>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ServiceTicket>>(Tickets
            .Where(x => x.OrganizationId == organizationId && x.AssetId == assetId)
            .OrderByDescending(x => x.OpenedAt)
            .ToList());

    public Task<(IReadOnlyList<ServiceTicket> Items, int Total)> ListPagedAsync(Guid organizationId, ServiceTicketStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Tickets
            .Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status.Value))
            .OrderByDescending(x => x.OpenedAt)
            .ToList();
        var total = rows.Count;
        var items = rows
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();
        return Task.FromResult<(IReadOnlyList<ServiceTicket>, int)>((items, total));
    }

    public Task<(IReadOnlyList<ServiceTicket> Items, int Total)> ListPagedScopedAsync(Guid organizationId, ServiceTicketStatus? status, int page, int pageSize, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        // Test fake has no asset navigation. Scope tests populate AllowedScopedAssetIds explicitly.
        var rows = Tickets
            .Where(x => x.OrganizationId == organizationId && AllowedScopedAssetIds.Contains(x.AssetId) && (!status.HasValue || x.Status == status.Value))
            .OrderByDescending(x => x.OpenedAt)
            .ToList();
        var total = rows.Count;
        var items = rows.Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100)).Take(Math.Clamp(pageSize, 1, 100)).ToList();
        return Task.FromResult<(IReadOnlyList<ServiceTicket>, int)>((items, total));
    }

    public HashSet<Guid> AllowedScopedAssetIds { get; } = [];

    public void Add(ServiceTicket ticket) => Tickets.Add(ticket);
}

public sealed class InMemoryAssetEvidenceRepository : IAssetEvidenceRepository
{
    public List<AssetEvidence> Items { get; } = [];

    public Task<IReadOnlyList<AssetEvidence>> ListContentByOffboardingItemAsync(Guid organizationId, Guid offboardingItemId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetEvidence>>(Items.Where(x => x.OrganizationId == organizationId && x.OffboardingItemId == offboardingItemId).ToList());

    public Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetEvidenceMetadata>>(Items.Where(x => x.OrganizationId == organizationId && x.AssetId == assetId).Select(ToMetadata).ToList());

    public Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssignmentIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> assignmentIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetEvidenceMetadata>>(Items.Where(x => x.OrganizationId == organizationId && x.AssignmentId.HasValue && assignmentIds.Contains(x.AssignmentId.Value)).Select(ToMetadata).ToList());

    public Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssignmentAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetEvidenceMetadata>>(Items.Where(x => x.OrganizationId == organizationId && x.AssignmentId == assignmentId).Select(ToMetadata).ToList());

    public Task<IReadOnlyList<AssetEvidenceRetentionCandidate>> ListRetentionCandidatesAsync(Guid organizationId, DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetEvidenceRetentionCandidate>>(Items
            .Where(x => x.OrganizationId == organizationId && !x.LegalHold && x.RedactedAt is null && x.UploadedAt <= cutoff)
            .OrderBy(x => x.UploadedAt)
            .Take(Math.Clamp(batchSize, 1, 2_000))
            .Select(x => new AssetEvidenceRetentionCandidate(x.Id, x.FileName)).ToList());

    public Task<int> RedactAsync(Guid organizationId, IReadOnlyCollection<Guid> evidenceIds, DateTimeOffset redactedAt, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var item in Items.Where(x => x.OrganizationId == organizationId && evidenceIds.Contains(x.Id) && !x.LegalHold && x.RedactedAt is null))
        {
            if (item.Redact(redactedAt)) count++;
        }
        return Task.FromResult(count);
    }

    public Task<AssetEvidence?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public Task<int> CountAsync(Guid organizationId, Guid assetId, EvidencePhase phase, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(x => x.OrganizationId == organizationId && x.AssetId == assetId && x.Phase == phase));

    public void Add(AssetEvidence evidence) => Items.Add(evidence);
    public void Remove(AssetEvidence evidence) => Items.Remove(evidence);

    private static AssetEvidenceMetadata ToMetadata(AssetEvidence x) =>
        new(x.Id, x.AssetId, x.AssignmentId, x.OffboardingItemId, x.AssetAuditItemId, x.Phase, x.FileName, x.ContentType, x.SizeBytes, x.Sha256, x.Caption, x.UploadedAt, x.UploadedBy, x.UploadedVia, x.LockedAt, x.LegalHold, x.RedactedAt);
}

public sealed class InMemoryAssetStatusSettingRepository : IAssetStatusSettingRepository
{
    public List<AssetStatusSetting> Items { get; } = [];

    public Task<IReadOnlyList<AssetStatusSetting>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssetStatusSetting>>(Items.Where(x => x.OrganizationId == organizationId).ToList());

    public Task<AssetStatusSetting?> GetByKeyAsync(Guid organizationId, string statusKey, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.OrganizationId == organizationId && x.StatusKey == statusKey));

    public void Add(AssetStatusSetting setting) => Items.Add(setting);
}

public sealed class FakeImageSanitizer : IImageSanitizer
{
    public SanitizedImage StripMetadata(DetectedImageFormat format, byte[] content)
    {
        var contentType = format switch
        {
            DetectedImageFormat.Png => "image/png",
            DetectedImageFormat.Webp => "image/webp",
            _ => "image/jpeg",
        };
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        return new SanitizedImage(content, contentType, content.LongLength, sha256);
    }
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid OrganizationId { get; set; } = Guid.NewGuid();
    public Guid? PersonId { get; set; }
    public string Subject { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = "tester@acme.test";
    public string Language { get; set; } = "pl";
    public string IpAddress { get; set; } = "127.0.0.1";
    public IReadOnlyCollection<string> Roles { get; set; } = ["owner"];
}

public sealed class InMemoryMaintenanceScheduleRepository : IMaintenanceScheduleRepository
{
    public List<MaintenanceSchedule> Items { get; } = [];

    public Task<IReadOnlyList<MaintenanceSchedule>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MaintenanceSchedule>>(Items.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.NextDueOn).ToList());

    public Task<IReadOnlyList<MaintenanceSchedule>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MaintenanceSchedule>>(Items.Where(x => x.OrganizationId == organizationId && x.AssetId == assetId).ToList());

    public Task<IReadOnlyList<MaintenanceSchedule>> ListDueAsync(Guid organizationId, DateOnly through, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MaintenanceSchedule>>(Items.Where(x => x.OrganizationId == organizationId && x.IsActive && x.NextDueOn <= through).OrderBy(x => x.NextDueOn).ToList());

    public Task<IReadOnlyDictionary<Guid, DateOnly>> GetEarliestDueByAssetAsync(Guid organizationId, IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, DateOnly>>(Items
            .Where(x => x.OrganizationId == organizationId && x.IsActive && assetIds.Contains(x.AssetId))
            .GroupBy(x => x.AssetId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.NextDueOn)));

    public Task<MaintenanceSchedule?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.OrganizationId == organizationId && x.Id == id));

    public void Add(MaintenanceSchedule schedule) => Items.Add(schedule);

    public void Remove(MaintenanceSchedule schedule) => Items.Remove(schedule);
}
