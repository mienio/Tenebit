using Tenebit.Application.Abstractions;

namespace Tenebit.Application.Common;

public sealed record ManagerAccessScope(IReadOnlySet<Guid> PersonIds, IReadOnlySet<Guid> TeamIds)
{
    public bool ContainsPerson(Guid personId) => PersonIds.Contains(personId);
    public bool ContainsAsset(Guid? assignedPersonId, Guid? teamId) =>
        (assignedPersonId.HasValue && PersonIds.Contains(assignedPersonId.Value))
        || (teamId.HasValue && TeamIds.Contains(teamId.Value));
}

// Resolves the row-level scope for a plain Manager. The link from the login to Person is stable
// (OrganizationUser.PersonId -> people(OrganizationId, Id)); e-mail is deliberately not used as an
// authorization key. Repository methods calculate the managed people/teams in SQL instead of loading
// the entire tenant and filtering it in application memory (AUD3-006).
public sealed class ManagerScopeService
{
    private readonly IPersonRepository _people;
    private readonly ITeamRepository _teams;

    public ManagerScopeService(IPersonRepository people, ITeamRepository teams)
    {
        _people = people;
        _teams = teams;
    }

    // Null means the actor has an organization-wide role for this module. A non-null, possibly empty,
    // scope means Manager authorization applies. An unlinked Manager is fail-closed with an empty scope.
    public async Task<ManagerAccessScope?> ResolveAsync(ICurrentUser currentUser, IReadOnlyCollection<string> orgWideRoles, CancellationToken cancellationToken)
    {
        if (currentUser.HasAnyRole(orgWideRoles.ToArray())) return null;
        if (!currentUser.HasAnyRole(TenebitRoles.Manager)) return null;
        if (currentUser.PersonId is not { } managerPersonId)
        {
            return new ManagerAccessScope(new HashSet<Guid>(), new HashSet<Guid>());
        }

        var organizationId = currentUser.OrganizationId;
        var teamIds = (await _teams.ListManagedIdsAsync(organizationId, managerPersonId, cancellationToken)).ToHashSet();
        var personIds = (await _people.ListManagedScopePersonIdsAsync(organizationId, managerPersonId, teamIds, cancellationToken)).ToHashSet();
        return new ManagerAccessScope(personIds, teamIds);
    }
}
