using Tenebit.Application.Abstractions;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;

namespace Tenebit.Application.Offboarding;

/// <summary>Shared "dezaktywuj + zwolnij zaplanowane licencje" krok (spec 4.5 krok 9, 4.12) używany zarówno przez
/// cykliczny <see cref="Tenebit.Application.People.PersonOffboardingSchedulerService"/>, jak i ręczny
/// <c>POST /api/offboarding/{id}/execute-scheduled-actions</c>. Idempotentne i bezpieczne do wielokrotnego
/// wywołania — błąd pojedynczego zwolnienia licencji jest izolowany (try/catch per pozycja) i widoczny jako
/// <see cref="OffboardingItem.RecordAutomationFailure"/>, do ponowienia przy kolejnym wywołaniu; nigdy nie cofa
/// dezaktywacji osoby ani innych udanych zwolnień. Zapis następuje raz, na koniec przetwarzania jednej osoby, więc
/// błąd innej osoby (przetwarzanej w osobnym wywołaniu) nigdy nie wycofuje tego zapisu.</summary>
public sealed class OffboardingScheduledActionsService
{
    private readonly IOffboardingCaseRepository _cases;
    private readonly IOffboardingItemRepository _items;
    private readonly ILicenseRepository _licenses;
    private readonly IActivityLogRepository _activity;
    private readonly IUnitOfWork _unitOfWork;

    public OffboardingScheduledActionsService(IOffboardingCaseRepository cases, IOffboardingItemRepository items, ILicenseRepository licenses, IActivityLogRepository activity, IUnitOfWork unitOfWork)
    {
        _cases = cases;
        _items = items;
        _licenses = licenses;
        _activity = activity;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(Guid organizationId, Person person, DateTimeOffset now, string actorSubject, CancellationToken cancellationToken)
    {
        var hasChanges = false;

        if (person.EmploymentStatus == EmploymentStatus.Offboarding && person.EmploymentEndsAt.HasValue && person.EmploymentEndsAt.Value <= now)
        {
            person.Deactivate(now);
            _activity.Add(new ActivityLog(organizationId, "person.deactivated", "person", person.Id, actorSubject, person.FullName, now));
            hasChanges = true;
        }

        // Nie jest jeszcze (i wciąż nie jest) czas dezaktywacji tej osoby — nic więcej do zrobienia w tym wywołaniu.
        if (person.EmploymentStatus != EmploymentStatus.Inactive)
        {
            if (hasChanges) await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var offboardingCase = await _cases.FindOpenByPersonAsync(organizationId, person.Id, cancellationToken);
        if (offboardingCase is null)
        {
            // Legacy fallback: offboarding rozpoczęty zanim istniał OffboardingCase — zachowaj poprzednie
            // zachowanie zwalniania wszystkich miejsc licencyjnych bezpośrednio.
            hasChanges |= await ReleaseAllSeatsAsync(organizationId, person, now, actorSubject, cancellationToken);
            if (hasChanges) await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        offboardingCase.MarkPersonDeactivated(now);

        var scheduledItems = (await _items.ListByCaseAsync(organizationId, offboardingCase.Id, cancellationToken))
            .Where(i => i.Type == OffboardingItemType.LicenseRelease && i.AutomationMode == OffboardingItemAutomationMode.AtEmploymentEnd)
            .ToList();

        if (scheduledItems.Count > 0)
        {
            var licenses = await _licenses.ListAsync(organizationId, cancellationToken);
            foreach (var item in scheduledItems.Where(i => !i.IsResolved))
            {
                try
                {
                    var license = licenses.FirstOrDefault(l => l.Id == item.LicenseId)
                        ?? throw new InvalidOperationException("Licencja nie istnieje.");
                    license.UnassignSeat(person.Id);
                    item.MarkReleased(now, actorSubject);
                    _activity.Add(new ActivityLog(organizationId, "offboarding.license_released", "offboarding_item", item.Id, actorSubject, license.Name, now));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    item.RecordAutomationFailure(now, ex.Message);
                    _activity.Add(new ActivityLog(organizationId, "offboarding.license_release_failed", "offboarding_item", item.Id, actorSubject, ex.Message, now));
                }
            }
        }

        // "Zaplanowane działania zakończone" oznacza, że KAŻDA pozycja AtEmploymentEnd faktycznie się powiodła —
        // nie tylko że została podjęta próba. Jeśli którakolwiek nadal ma błąd/stan nieostateczny, znacznik
        // zostaje nieustawiony, aby kolejny cykl (lub ręczne ponowienie) spróbował ponownie.
        if (scheduledItems.All(i => i.IsResolved))
        {
            offboardingCase.MarkScheduledActionsCompleted(now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ReleaseAllSeatsAsync(Guid organizationId, Person person, DateTimeOffset now, string actorSubject, CancellationToken cancellationToken)
    {
        var hasChanges = false;
        var licenses = await _licenses.ListAsync(organizationId, cancellationToken);
        foreach (var license in licenses.Where(l => l.Seats.Any(s => s.PersonId == person.Id)))
        {
            try
            {
                license.UnassignSeat(person.Id);
                _activity.Add(new ActivityLog(organizationId, "license.seat_unassigned", "license", license.Id, actorSubject, license.Name, now));
                hasChanges = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _activity.Add(new ActivityLog(organizationId, "license.seat_unassign_failed", "license", license.Id, actorSubject, ex.Message, now));
                hasChanges = true;
            }
        }

        return hasChanges;
    }
}
