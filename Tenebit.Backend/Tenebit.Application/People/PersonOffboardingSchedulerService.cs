using Tenebit.Application.Abstractions;
using Tenebit.Domain.Audit;
using Tenebit.Domain.People;

namespace Tenebit.Application.People;

public sealed class PersonOffboardingSchedulerService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IPersonRepository _people;
    private readonly ILicenseRepository _licenses;
    private readonly IActivityLogRepository _activity;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PersonOffboardingSchedulerService(IOrganizationRepository organizations, IPersonRepository people, ILicenseRepository licenses, IActivityLogRepository activity, IClock clock, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _people = people;
        _licenses = licenses;
        _activity = activity;
        _clock = clock;
        _unitOfWork = unitOfWork;
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
        var due = (await _people.ListAsync(organizationId, null, cancellationToken))
            .Where(p => p.EmploymentStatus == EmploymentStatus.Offboarding && p.EmploymentEndsAt.HasValue && p.EmploymentEndsAt.Value <= now)
            .ToList();
        if (due.Count == 0) return;

        var licenses = await _licenses.ListAsync(organizationId, cancellationToken);
        var hasChanges = false;

        foreach (var person in due)
        {
            person.Deactivate(now);
            _activity.Add(new ActivityLog(organizationId, "person.deactivated", "person", person.Id, "system", person.FullName, now));
            hasChanges = true;

            foreach (var license in licenses.Where(l => l.Seats.Any(s => s.PersonId == person.Id)))
            {
                license.UnassignSeat(person.Id);
                _activity.Add(new ActivityLog(organizationId, "license.seat_unassigned", "license", license.Id, "system", license.Name, now));
            }
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
