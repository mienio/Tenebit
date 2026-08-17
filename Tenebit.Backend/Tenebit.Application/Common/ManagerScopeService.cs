using Tenebit.Application.Abstractions;

namespace Tenebit.Application.Common;

// Resolves which Person records a Manager may see when they hold no organization-wide viewer role.
// The declared role model promises Manager "widok zespołu" (own team only), but every list/detail
// endpoint used to return the whole organization once the Manager role passed the module gate
// (audyt AUD3-006: brak centralnej autoryzacji zasobowej). Callers stay privileged (org-wide) when
// the actor also holds any of the module's other viewer roles — this only narrows plain Manager.
public sealed class ManagerScopeService
{
    private readonly IPersonRepository _people;
    private readonly ITeamRepository _teams;

    public ManagerScopeService(IPersonRepository people, ITeamRepository teams)
    {
        _people = people;
        _teams = teams;
    }

    // Null means "no scoping" (actor is not a plain Manager, e.g. Owner/Admin/Hr/etc — show everything
    // the module normally allows). A non-null set is the Manager's own person plus their team's members.
    public async Task<HashSet<Guid>?> ResolveVisiblePersonIdsAsync(ICurrentUser currentUser, IReadOnlyCollection<string> orgWideRoles, CancellationToken cancellationToken)
    {
        if (currentUser.HasAnyRole(orgWideRoles.ToArray())) return null;
        if (!currentUser.HasAnyRole(TenebitRoles.Manager)) return null;

        var organizationId = currentUser.OrganizationId;
        var managerPerson = string.IsNullOrEmpty(currentUser.Email) ? null : await _people.FindByEmailAsync(organizationId, currentUser.Email, cancellationToken);
        var visible = new HashSet<Guid>();
        if (managerPerson is null) return visible;

        visible.Add(managerPerson.Id);

        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var managedTeamIds = teams.Where(t => t.ManagerId == managerPerson.Id).Select(t => t.Id).ToHashSet();

        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        foreach (var person in people)
        {
            if (person.ManagerId == managerPerson.Id || (person.TeamId is { } teamId && managedTeamIds.Contains(teamId)))
            {
                visible.Add(person.Id);
            }
        }

        return visible;
    }
}
