using Tenebit.Application.Abstractions;
using Tenebit.Application.Offboarding;
using Tenebit.Domain.People;

namespace Tenebit.Application.People;

/// <summary>Cykliczny scheduler dla dezaktywacji osób w trakcie offboardingu i zwolnienia ich zaplanowanych
/// (AtEmploymentEnd) miejsc licencyjnych. Faktyczna logika "jedna osoba" żyje w
/// <see cref="OffboardingScheduledActionsService"/> - współdzielona z ręcznym endpointem
/// <c>POST /api/offboarding/{id}/execute-scheduled-actions</c>, żeby nie duplikować kroków ani izolacji błędów.</summary>
public sealed class PersonOffboardingSchedulerService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IPersonRepository _people;
    private readonly IOffboardingCaseRepository _cases;
    private readonly OffboardingScheduledActionsService _scheduledActions;
    private readonly IClock _clock;

    public PersonOffboardingSchedulerService(IOrganizationRepository organizations, IPersonRepository people, IOffboardingCaseRepository cases, OffboardingScheduledActionsService scheduledActions, IClock clock)
    {
        _organizations = organizations;
        _people = people;
        _cases = cases;
        _scheduledActions = scheduledActions;
        _clock = clock;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (var organization in await _organizations.ListAllAsync(cancellationToken))
        {
            await ProcessOrganizationAsync(organization.Id, cancellationToken);
        }
    }

    private async Task ProcessOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var people = await _people.ListAsync(organizationId, null, cancellationToken);

        // Two groups need a pass: people whose planned end date has just arrived (about to be deactivated),
        // and already-deactivated people whose scheduled license releases haven't all succeeded yet (retry).
        var dueForDeactivation = people.Where(p => p.EmploymentStatus == EmploymentStatus.Offboarding && p.EmploymentEndsAt.HasValue && p.EmploymentEndsAt.Value <= now);
        var candidatesForRetry = people.Where(p => p.EmploymentStatus == EmploymentStatus.Inactive);

        foreach (var person in dueForDeactivation.Concat(candidatesForRetry).Distinct())
        {
            if (person.EmploymentStatus == EmploymentStatus.Inactive)
            {
                var openCase = await _cases.FindOpenByPersonAsync(organizationId, person.Id, cancellationToken);
                if (openCase is null || openCase.ScheduledActionsCompletedAt.HasValue) continue;
            }

            await _scheduledActions.ExecuteAsync(organizationId, person, now, "system", cancellationToken);
        }
    }
}
