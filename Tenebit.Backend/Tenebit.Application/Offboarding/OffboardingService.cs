using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;

namespace Tenebit.Application.Offboarding;

/// <summary>Tworzenie, uruchamianie i rozliczanie spraw offboardingowych (spec 4.5). Publiczny token i protokół
/// PDF są poza zakresem — przyjdą w kolejnym zadaniu.</summary>
public sealed class OffboardingService
{
    private static readonly AssignmentStatus[] OpenAssignmentStatuses =
        [AssignmentStatus.AwaitingAcceptance, AssignmentStatus.Accepted, AssignmentStatus.Overdue, AssignmentStatus.PartiallyReturned];

    private readonly IOffboardingCaseRepository _cases;
    private readonly IOffboardingItemRepository _items;
    private readonly IPersonRepository _people;
    private readonly IAssetRepository _assets;
    private readonly IAssetCategoryRepository _categories;
    private readonly IAssignmentRepository _assignments;
    private readonly ILicenseRepository _licenses;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OffboardingScheduledActionsService _scheduledActions;
    private readonly AssetReturnDispositionService _disposition;
    private readonly AssetInspectionService _inspectionService;
    private readonly IAssetInspectionRepository _inspections;

    public OffboardingService(IOffboardingCaseRepository cases, IOffboardingItemRepository items, IPersonRepository people,
        IAssetRepository assets, IAssetCategoryRepository categories, IAssignmentRepository assignments, ILicenseRepository licenses,
        IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork,
        OffboardingScheduledActionsService scheduledActions, AssetReturnDispositionService disposition, AssetInspectionService inspectionService,
        IAssetInspectionRepository inspections)
    {
        _cases = cases;
        _items = items;
        _people = people;
        _assets = assets;
        _categories = categories;
        _assignments = assignments;
        _licenses = licenses;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _scheduledActions = scheduledActions;
        _disposition = disposition;
        _inspectionService = inspectionService;
        _inspections = inspections;
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

    /// <summary>Fizyczne przyjęcie zwrotu pozycji AssetReturn (spec 4.5 krok 10). Stosuje politykę zwrotu kategorii
    /// aktywa — DirectToStock i InspectionRequired reużywają <see cref="AssetReturnDispositionService"/> (ta sama
    /// logika co w AssignmentService). ReturnToVendor/Dispose są tu uproszczone do bezpośredniej zmiany statusu
    /// aktywa — pełny przepływ przekazania do zewnętrznego odbiorcy to osobny, przyszły temat (YAGNI na razie).</summary>
    public async Task<Result<OffboardingCaseDetailsResponse>> ConfirmItemReturnAsync(Guid id, Guid itemId, ConfirmOffboardingItemReturnRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var item = await _items.GetAsync(organizationId, id, itemId, cancellationToken);
        if (item is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Pozycja nie istnieje."));
        if (item.Type != OffboardingItemType.AssetReturn || item.AssetId is null)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Ta pozycja nie dotyczy zwrotu aktywa."));
        }

        var asset = await _assets.GetAsync(organizationId, item.AssetId.Value, cancellationToken);
        if (asset is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
        var category = await _categories.GetAsync(organizationId, asset.CategoryId, cancellationToken);

        try
        {
            var now = _clock.UtcNow;
            item.MarkReceived(now, _currentUser.Subject);

            var disposition = _disposition.ApplyPhysicalReturn(asset, category, request.ReturnLocation, organizationId, now, _currentUser.Subject, item.AssignmentId, item.Id);
            switch (disposition)
            {
                case AssetReturnDisposition.InspectionRequired:
                    // Kontrola trwa — pozycja czeka na complete-inspection, nie ma jeszcze stanu końcowego.
                    break;
                case AssetReturnDisposition.ReturnToVendor:
                case AssetReturnDisposition.Disposed:
                case AssetReturnDisposition.DirectToStock:
                    item.CompleteInspection(now, _currentUser.Subject);
                    break;
            }

            _activity.Add(new ActivityLog(organizationId, "offboarding.asset_returned", "asset", asset.Id, _currentUser.Subject, asset.Name, now));

            var items = await _items.ListByCaseAsync(organizationId, id, cancellationToken);
            offboardingCase.RecomputeStatus(items, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Kończy kontrolę powiązaną z pozycją InspectionRequired — reużywa <see cref="AssetInspectionService.CompleteAsync"/>
    /// (nie duplikuje logiki zmiany statusu aktywa), a następnie rozlicza pozycję offboardingu wg wyniku kontroli.</summary>
    public async Task<Result<OffboardingCaseDetailsResponse>> CompleteItemInspectionAsync(Guid id, Guid itemId, CompleteAssetInspectionRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var item = await _items.GetAsync(organizationId, id, itemId, cancellationToken);
        if (item is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Pozycja nie istnieje."));
        if (item.IsResolved) return await BuildDetailsAsync(id, cancellationToken);
        if (item.AssetId is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Ta pozycja nie dotyczy aktywa."));

        var inspection = await _inspections.GetPendingByAssetAsync(organizationId, item.AssetId.Value, cancellationToken);
        if (inspection is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Brak oczekującej kontroli dla tego aktywa."));

        var inspectionResult = await _inspectionService.CompleteAsync(inspection.Id, request, cancellationToken);
        if (inspectionResult.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(inspectionResult.Error!);

        try
        {
            var now = _clock.UtcNow;
            if (request.Outcome == InspectionOutcome.ReadyForReuse)
            {
                item.CompleteInspection(now, _currentUser.Subject);
            }
            else
            {
                var status = request.Outcome switch
                {
                    InspectionOutcome.Damaged => OffboardingItemStatus.Damaged,
                    _ => OffboardingItemStatus.Retained // Retired/Disposed — aktywo fizycznie obecne, ale nie wraca do obiegu.
                };
                item.Resolve(status, request.Notes ?? request.DamageAssessmentNotes ?? "Wynik kontroli", _currentUser.Subject, now);
            }

            _activity.Add(new ActivityLog(organizationId, "offboarding.asset_inspection_completed", "offboarding_item", item.Id, _currentUser.Subject, item.Label, now));

            var items = await _items.ListByCaseAsync(organizationId, id, cancellationToken);
            offboardingCase.RecomputeStatus(items, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Ręczne zwolnienie miejsca licencyjnego dla pozycji LicenseRelease — dla trybu Manual albo gdy
    /// administrator chce wyprzedzić automatykę AtEmploymentEnd.</summary>
    public async Task<Result<OffboardingCaseDetailsResponse>> ReleaseItemLicenseAsync(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var item = await _items.GetAsync(organizationId, id, itemId, cancellationToken);
        if (item is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Pozycja nie istnieje."));
        if (item.Type != OffboardingItemType.LicenseRelease || item.LicenseId is null)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Ta pozycja nie dotyczy zwolnienia licencji."));
        }

        if (item.IsResolved) return await BuildDetailsAsync(id, cancellationToken);

        var license = await _licenses.GetAsync(organizationId, item.LicenseId.Value, cancellationToken);
        if (license is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Licencja nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            license.UnassignSeat(offboardingCase.PersonId);
            item.MarkReleased(now, _currentUser.Subject);
            _activity.Add(new ActivityLog(organizationId, "offboarding.license_released", "offboarding_item", item.Id, _currentUser.Subject, item.Label, now));

            var items = await _items.ListByCaseAsync(organizationId, id, cancellationToken);
            offboardingCase.RecomputeStatus(items, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Jawne rozstrzygnięcie Missing/Damaged/Retained dla pozycji AssetReturn — moment, w którym operator
    /// zatwierdza status aktywa; sama odpowiedź pracownika (spec 2.3) tego nie robi automatycznie.</summary>
    public async Task<Result<OffboardingCaseDetailsResponse>> ResolveItemAsync(Guid id, Guid itemId, ResolveOffboardingItemRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var item = await _items.GetAsync(organizationId, id, itemId, cancellationToken);
        if (item is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Pozycja nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            item.Resolve(request.Status, request.Notes, _currentUser.Subject, now);

            if (item.AssetId is not null)
            {
                var asset = await _assets.GetAsync(organizationId, item.AssetId.Value, cancellationToken);
                if (asset is not null)
                {
                    switch (request.Status)
                    {
                        case OffboardingItemStatus.Missing:
                            asset.ReleaseAssignment(AssetStatus.Lost);
                            _activity.Add(new ActivityLog(organizationId, "offboarding.asset_missing", "asset", asset.Id, _currentUser.Subject, asset.Name, now));
                            break;
                        case OffboardingItemStatus.Damaged:
                            asset.ReleaseAssignment(AssetStatus.Damaged);
                            _activity.Add(new ActivityLog(organizationId, "offboarding.asset_damaged", "asset", asset.Id, _currentUser.Subject, asset.Name, now));
                            break;
                        case OffboardingItemStatus.Retained:
                            asset.ChangeStatus(AssetStatus.Retired);
                            _activity.Add(new ActivityLog(organizationId, "offboarding.asset_available", "asset", asset.Id, _currentUser.Subject, asset.Name, now));
                            break;
                    }
                }
            }

            var items = await _items.ListByCaseAsync(organizationId, id, cancellationToken);
            offboardingCase.RecomputeStatus(items, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> WaiveItemAsync(Guid id, Guid itemId, WaiveOffboardingItemRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var item = await _items.GetAsync(organizationId, id, itemId, cancellationToken);
        if (item is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Pozycja nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            item.Waive(request.Reason, _currentUser.Subject, now);
            _activity.Add(new ActivityLog(organizationId, "offboarding.item_waived", "offboarding_item", item.Id, _currentUser.Subject, item.Label, now));

            var items = await _items.ListByCaseAsync(organizationId, id, cancellationToken);
            offboardingCase.RecomputeStatus(items, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            var protocolNumber = offboardingCase.FinalProtocolNumber ?? CreateProtocolNumber(now);
            offboardingCase.Complete(now, _currentUser.Subject, protocolNumber);
            _activity.Add(new ActivityLog(organizationId, "offboarding.completed", "offboarding_case", offboardingCase.Id, _currentUser.Subject, protocolNumber, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Anuluje sprawę i — dla nieodebranych pozycji AssetReturn — przywraca aktywo do statusu Assigned
    /// dla tej samej osoby (spec 4.4). Rezerwacje nie są odtwarzane (moduł nie istnieje jeszcze).</summary>
    public async Task<Result<OffboardingCaseDetailsResponse>> CancelAsync(Guid id, CancelOffboardingCaseRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var person = await _people.GetAsync(organizationId, offboardingCase.PersonId, cancellationToken);

        try
        {
            var now = _clock.UtcNow;
            offboardingCase.Cancel(now, request.Reason);

            if (person is not null && person.EmploymentStatus != EmploymentStatus.Active)
            {
                person.Activate();
            }

            var items = await _items.ListByCaseAsync(organizationId, id, cancellationToken);
            foreach (var item in items.Where(x => x.Type == OffboardingItemType.AssetReturn && !x.IsResolved && x.AssetId is not null))
            {
                var asset = await _assets.GetAsync(organizationId, item.AssetId!.Value, cancellationToken);
                if (asset is null) continue;
                asset.RestorePendingReturn(offboardingCase.PersonId);
            }

            _activity.Add(new ActivityLog(organizationId, "offboarding.cancelled", "offboarding_case", offboardingCase.Id, _currentUser.Subject, request.Reason, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> RestoreEmploymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var person = await _people.GetAsync(organizationId, offboardingCase.PersonId, cancellationToken);

        try
        {
            var now = _clock.UtcNow;
            offboardingCase.RestoreEmployment(now);
            person?.Activate();
            _activity.Add(new ActivityLog(organizationId, "offboarding.restored", "offboarding_case", offboardingCase.Id, _currentUser.Subject, person?.FullName, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await BuildDetailsAsync(id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    private static string CreateProtocolNumber(DateTimeOffset now) => $"OFF-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

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
