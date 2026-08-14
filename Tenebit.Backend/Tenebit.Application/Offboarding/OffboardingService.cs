using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;

namespace Tenebit.Application.Offboarding;

/// <summary>Tworzenie i uruchamianie spraw offboardingowych (spec 4.5 kroki 1-7). Rozliczanie pozycji, zamykanie,
/// anulowanie, publiczny token i protokół PDF są poza zakresem — przyjdą w kolejnych zadaniach.</summary>
public sealed class OffboardingService
{
    private static readonly AssignmentStatus[] OpenAssignmentStatuses =
        [AssignmentStatus.AwaitingAcceptance, AssignmentStatus.Accepted, AssignmentStatus.Overdue, AssignmentStatus.PartiallyReturned];

    private readonly IOffboardingCaseRepository _cases;
    private readonly IOffboardingItemRepository _items;
    private readonly IPersonRepository _people;
    private readonly IAssetRepository _assets;
    private readonly IAssignmentRepository _assignments;
    private readonly ILicenseRepository _licenses;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OffboardingScheduledActionsService _scheduledActions;

    public OffboardingService(IOffboardingCaseRepository cases, IOffboardingItemRepository items, IPersonRepository people,
        IAssetRepository assets, IAssignmentRepository assignments, ILicenseRepository licenses,
        IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork,
        OffboardingScheduledActionsService scheduledActions)
    {
        _cases = cases;
        _items = items;
        _people = people;
        _assets = assets;
        _assignments = assignments;
        _licenses = licenses;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _scheduledActions = scheduledActions;
    }

    public async Task<Result<PagedResult<OffboardingCaseResponse>>> ListPagedAsync(OffboardingCaseStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<PagedResult<OffboardingCaseResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var (cases, total) = await _cases.ListPagedAsync(organizationId, status, page, pageSize, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var names = people.ToDictionary(x => x.Id, x => x.FullName);
        return Result<PagedResult<OffboardingCaseResponse>>.Success(new PagedResult<OffboardingCaseResponse>(cases.Select(x => Map(x, names)).ToList(), total, page, pageSize));
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        return await BuildDetailsAsync(id, cancellationToken);
    }

    private async Task<Result<OffboardingCaseDetailsResponse>> BuildDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        return Result<OffboardingCaseDetailsResponse>.Success(await BuildDetailsAsync(offboardingCase, cancellationToken));
    }

    private async Task<OffboardingCaseDetailsResponse> BuildDetailsAsync(OffboardingCase offboardingCase, CancellationToken cancellationToken)
    {
        var person = await _people.GetAsync(offboardingCase.OrganizationId, offboardingCase.PersonId, cancellationToken);
        var items = await _items.ListByCaseAsync(offboardingCase.OrganizationId, offboardingCase.Id, cancellationToken);
        var names = person is null ? new Dictionary<Guid, string>() : new Dictionary<Guid, string> { [person.Id] = person.FullName };
        return new OffboardingCaseDetailsResponse(Map(offboardingCase, names), items.Select(MapItem).ToList());
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> CreateAsync(CreateOffboardingCaseRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var person = await _people.GetAsync(organizationId, request.PersonId, cancellationToken);
        if (person is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Wybrana osoba nie istnieje."));
        if (person.EmploymentStatus != EmploymentStatus.Active)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Sprawę offboardingową można utworzyć tylko dla aktywnej osoby."));
        }

        if (await _cases.FindOpenByPersonAsync(organizationId, person.Id, cancellationToken) is not null)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Conflict("Dla tej osoby istnieje już aktywna sprawa offboardingowa."));
        }

        try
        {
            var offboardingCase = new OffboardingCase(organizationId, person.Id, request.EmploymentEndsAt, request.ReturnDueDate,
                request.DefaultReturnLocation, request.Notes, request.ProcessOwnerId,
                request.BlockNewReservations, request.CancelFutureReservations, request.AutoReleaseLicenses,
                _currentUser.Subject, _clock.UtcNow);

            _cases.Add(offboardingCase);
            _activity.Add(new ActivityLog(organizationId, "offboarding.created", "offboarding_case", offboardingCase.Id, _currentUser.Subject, person.FullName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await BuildDetailsAsync(offboardingCase.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> UpdateAsync(Guid id, UpdateOffboardingCaseRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        try
        {
            offboardingCase.UpdateDraft(request.EmploymentEndsAt, request.ReturnDueDate, request.DefaultReturnLocation, request.Notes,
                request.ProcessOwnerId, request.BlockNewReservations, request.CancelFutureReservations, request.AutoReleaseLicenses);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> StartAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var person = await _people.GetAsync(organizationId, offboardingCase.PersonId, cancellationToken);
        if (person is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Osoba przypisana do sprawy nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            offboardingCase.Start(now);

            // Aktywa aktualnie przypisane osobie (bezpośrednio lub przez trwające wydanie) — jeden wpis na aktywo,
            // z opcjonalnym powiązaniem AssignmentId, jeśli aktywo pochodzi z otwartego wydania.
            var heldAssets = (await _assets.ListAsync(organizationId, null, null, null, cancellationToken))
                .Where(a => a.AssignedPersonId == person.Id)
                .ToList();

            var openAssignments = (await _assignments.ListAsync(organizationId, cancellationToken))
                .Where(a => a.PersonId == person.Id && OpenAssignmentStatuses.Contains(a.Status))
                .ToList();

            var assignmentIdByAssetId = new Dictionary<Guid, Guid>();
            foreach (var assignment in openAssignments)
            {
                foreach (var asset in assignment.Assets.Where(x => x.ReturnResolution is null))
                {
                    assignmentIdByAssetId.TryAdd(asset.AssetId, assignment.Id);
                }
            }

            var sortOrder = 0;
            foreach (var asset in heldAssets)
            {
                var assignmentId = assignmentIdByAssetId.TryGetValue(asset.Id, out var found) ? found : (Guid?)null;
                var item = new OffboardingItem(organizationId, offboardingCase.Id, OffboardingItemType.AssetReturn,
                    $"{asset.Name} ({asset.AssetTag})", true, asset.Id, assignmentId, null, OffboardingItemAutomationMode.Manual, sortOrder++);
                _items.Add(item);

                asset.MarkPendingReturn();
                _activity.Add(new ActivityLog(organizationId, "offboarding.asset_marked_pending_return", "asset", asset.Id, _currentUser.Subject, asset.Name, now));
            }

            // Miejsca licencyjne przypisane osobie — nieobowiązkowe do zwolnienia, chyba że sprawa ma włączone auto-zwalnianie.
            var licenses = (await _licenses.ListAsync(organizationId, cancellationToken))
                .Where(l => l.Seats.Any(s => s.PersonId == person.Id))
                .ToList();

            var automationMode = offboardingCase.AutoReleaseLicenses ? OffboardingItemAutomationMode.AtEmploymentEnd : OffboardingItemAutomationMode.Manual;
            foreach (var license in licenses)
            {
                var item = new OffboardingItem(organizationId, offboardingCase.Id, OffboardingItemType.LicenseRelease,
                    license.Name, false, null, null, license.Id, automationMode, sortOrder++);
                _items.Add(item);
            }

            person.StartOffboarding(offboardingCase.EmploymentEndsAt);
            _activity.Add(new ActivityLog(organizationId, "offboarding.person_marked_offboarding", "person", person.Id, _currentUser.Subject, person.FullName, now));
            _activity.Add(new ActivityLog(organizationId, "offboarding.started", "offboarding_case", offboardingCase.Id, _currentUser.Subject, person.FullName, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(offboardingCase.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Ręczny odpowiednik jednego przebiegu <see cref="Tenebit.Application.People.PersonOffboardingSchedulerService"/>
    /// dla pojedynczej sprawy — dezaktywuje osobę (jeśli termin już minął) i próbuje zwolnić zaplanowane
    /// (AtEmploymentEnd) miejsca licencyjne. Idempotentny: bezpieczny do wielokrotnego wywołania, w tym jako
    /// ponowienie po naprawieniu przyczyny wcześniejszego błędu zwolnienia licencji.</summary>
    public async Task<Result<OffboardingCaseDetailsResponse>> ExecuteScheduledActionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var person = await _people.GetAsync(organizationId, offboardingCase.PersonId, cancellationToken);
        if (person is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Osoba przypisana do sprawy nie istnieje."));

        await _scheduledActions.ExecuteAsync(organizationId, person, _clock.UtcNow, _currentUser.Subject, cancellationToken);
        return await BuildDetailsAsync(id, cancellationToken);
    }

    private static OffboardingCaseResponse Map(OffboardingCase offboardingCase, IReadOnlyDictionary<Guid, string> personNames) =>
        new(
            offboardingCase.Id,
            offboardingCase.PersonId,
            personNames.GetValueOrDefault(offboardingCase.PersonId),
            offboardingCase.Status,
            offboardingCase.EmploymentEndsAt,
            offboardingCase.ReturnDueDate,
            offboardingCase.DefaultReturnLocation,
            offboardingCase.Notes,
            offboardingCase.ProcessOwnerId,
            offboardingCase.BlockNewReservations,
            offboardingCase.CancelFutureReservations,
            offboardingCase.AutoReleaseLicenses,
            offboardingCase.PersonDeactivatedAt,
            offboardingCase.ScheduledActionsCompletedAt,
            offboardingCase.CreatedAt,
            offboardingCase.CreatedBy,
            offboardingCase.StartedAt,
            offboardingCase.CompletedAt,
            offboardingCase.CompletedBy,
            offboardingCase.CancelledAt,
            offboardingCase.CancellationReason,
            offboardingCase.FinalProtocolNumber);

    private static OffboardingItemResponse MapItem(OffboardingItem item) =>
        new(
            item.Id,
            item.Type,
            item.AssetId,
            item.AssignmentId,
            item.LicenseId,
            item.Label,
            item.Required,
            item.Status,
            item.EmployeeResponse,
            item.EmployeeComment,
            item.AutomationMode,
            item.AutomationLastAttemptAt,
            item.AutomationError,
            item.ReceivedAt,
            item.ReceivedBy,
            item.InspectionCompletedAt,
            item.InspectionCompletedBy,
            item.ResolutionNotes,
            item.CompletedAt,
            item.CompletedBy,
            item.SortOrder);
}
