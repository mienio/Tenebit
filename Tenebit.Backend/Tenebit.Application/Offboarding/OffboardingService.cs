using Tenebit.Application.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Application.Evidence;
using Tenebit.Application.Identity;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;
using Tenebit.Domain.Reservations;
using Tenebit.Application.Reservations;
using Tenebit.Domain.Audits;

namespace Tenebit.Application.Offboarding;

/// <summary>Tworzenie, uruchamianie i rozliczanie spraw offboardingowych (spec 4.5), łącznie z publicznym
/// kanałem dla odchodzącego pracownika (token, odpowiedzi i upload zdjęć, spec 4.6).</summary>
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
    private readonly IOrganizationRepository _organizations;
    private readonly IEmailSender _emailSender;
    private readonly IEmailOutboxWriter? _emailOutbox;
    private readonly IAppLinkBuilder _linkBuilder;
    private readonly AssetEvidenceService _evidenceService;
    private readonly IEquipmentReservationRepository _reservations;
    private readonly IAssetAuditCampaignRepository _auditCampaigns;
    private readonly IAssetAuditItemRepository _auditItems;
    private readonly OffboardingResponseBuilder _responseBuilder;

    public OffboardingService(IOffboardingCaseRepository cases, IOffboardingItemRepository items, IPersonRepository people,
        IAssetRepository assets, IAssetCategoryRepository categories, IAssignmentRepository assignments, ILicenseRepository licenses,
        IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork,
        OffboardingScheduledActionsService scheduledActions, AssetReturnDispositionService disposition, AssetInspectionService inspectionService,
        IAssetInspectionRepository inspections, IOrganizationRepository organizations, IEmailSender emailSender, IAppLinkBuilder linkBuilder,
        AssetEvidenceService evidenceService, IEquipmentReservationRepository reservations,
        IAssetAuditCampaignRepository auditCampaigns, IAssetAuditItemRepository auditItems,
        OffboardingResponseBuilder responseBuilder, IEmailOutboxWriter? emailOutbox = null)
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
        _organizations = organizations;
        _emailSender = emailSender;
        _emailOutbox = emailOutbox;
        _linkBuilder = linkBuilder;
        _evidenceService = evidenceService;
        _reservations = reservations;
        _auditCampaigns = auditCampaigns;
        _auditItems = auditItems;
        _responseBuilder = responseBuilder;
    }


    public async Task<Result<PagedResult<OffboardingCaseResponse>>> ListPagedAsync(OffboardingCaseStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<PagedResult<OffboardingCaseResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var (cases, total) = await _cases.ListPagedAsync(organizationId, status, page, pageSize, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var names = people.ToDictionary(x => x.Id, x => x.FullName);
        return Result<PagedResult<OffboardingCaseResponse>>.Success(new PagedResult<OffboardingCaseResponse>(cases.Select(x => OffboardingResponseBuilder.Map(x, names)).ToList(), total, page, pageSize));
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingCaseDetailsResponse>.Failure(access.Error!);

        return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
    }

    /// <summary>Podsumowanie przed uruchomieniem sprawy (spec 4.5 krok 2) - wyłącznie odczyt, bez efektów ubocznych.</summary>
    public async Task<Result<OffboardingPreviewResponse>> GetPreviewAsync(Guid personId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<OffboardingPreviewResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var person = await _people.GetAsync(organizationId, personId, cancellationToken);
        if (person is null) return Result<OffboardingPreviewResponse>.Failure(Error.NotFound("Osoba nie istnieje."));

        var heldAssets = (await _assets.ListAsync(organizationId, null, null, null, cancellationToken))
            .Where(a => a.AssignedPersonId == person.Id)
            .Select(a => new OffboardingPreviewAssetResponse(a.Id, a.Name, a.AssetTag, a.Status))
            .ToList();

        var openAssignments = (await _assignments.ListAsync(organizationId, cancellationToken))
            .Where(a => a.PersonId == person.Id && OpenAssignmentStatuses.Contains(a.Status))
            .Select(a => new OffboardingPreviewAssignmentResponse(a.Id, a.ProtocolNumber, a.Status, a.IssuedAt))
            .ToList();

        var licenseSeats = (await _licenses.ListAsync(organizationId, cancellationToken))
            .Where(l => l.Seats.Any(s => s.PersonId == person.Id))
            .Select(l => new OffboardingPreviewLicenseResponse(l.Id, l.Name))
            .ToList();

        var reservations = await _responseBuilder.ListRelevantReservationsAsync(organizationId, person.Id, cancellationToken);

        var unresolvedAuditItems = new List<OffboardingPreviewAuditItemResponse>();
        var (campaigns, _) = await _auditCampaigns.ListPagedAsync(organizationId, null, 1, int.MaxValue, cancellationToken);
        foreach (var campaign in campaigns)
        {
            var campaignItems = await _auditItems.ListByCampaignAsync(organizationId, campaign.Id, cancellationToken);
            foreach (var auditItem in campaignItems.Where(x => x.ExpectedPersonId == person.Id
                && x.Response != AssetAuditResponse.Pending && x.Resolution == AssetAuditResolution.None))
            {
                var asset = await _assets.GetAsync(organizationId, auditItem.AssetId, cancellationToken);
                unresolvedAuditItems.Add(new OffboardingPreviewAuditItemResponse(auditItem.Id, asset?.Name ?? "-", asset?.AssetTag, campaign.Name, auditItem.Response));
            }
        }

        return Result<OffboardingPreviewResponse>.Success(new OffboardingPreviewResponse(
            person.Id, person.FullName, heldAssets, openAssignments, licenseSeats, reservations, unresolvedAuditItems));
    }

    /// <summary>Rezerwacje osoby istotne dla offboardingu: oczekujące na decyzję oraz zatwierdzone/nieodebrane/w trakcie
    /// z przyszłym końcem (te same kryteria, których używa StartAsync przy anulowaniu - spec 4.5/8.12).</summary>
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

        if (request.ProcessOwnerId.HasValue && await _people.GetAsync(organizationId, request.ProcessOwnerId.Value, cancellationToken) is null)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Wybrany właściciel procesu nie istnieje."));
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

            return await _responseBuilder.BuildDetailsAsync(offboardingCase.OrganizationId, offboardingCase.Id, cancellationToken);
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

        if (request.ProcessOwnerId.HasValue && await _people.GetAsync(organizationId, request.ProcessOwnerId.Value, cancellationToken) is null)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation("Wybrany właściciel procesu nie istnieje."));
        }

        try
        {
            offboardingCase.UpdateDraft(request.EmploymentEndsAt, request.ReturnDueDate, request.DefaultReturnLocation, request.Notes,
                request.ProcessOwnerId, request.BlockNewReservations, request.CancelFutureReservations, request.AutoReleaseLicenses);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> StartAsync(Guid id, StartOffboardingCaseRequest request, CancellationToken cancellationToken)
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

            // Aktywa aktualnie przypisane osobie (bezpośrednio lub przez trwające wydanie) - jeden wpis na aktywo,
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

            // Miejsca licencyjne przypisane osobie - nieobowiązkowe do zwolnienia, chyba że sprawa ma włączone auto-zwalnianie.
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

            // Spec 4.5/8.12: przy starcie offboardingu anuluj przyszłe zatwierdzone rezerwacje i odrzuć oczekujące
            // wnioski tej osoby (jeśli sprawa tak konfiguruje). BlockNewReservations jest efektywnie pokryte przez
            // blokadę nowych wniosków dla osób nieaktywnych w ReservationService.
            if (offboardingCase.CancelFutureReservations)
            {
                var personReservations = await _reservations.ListByRequesterAsync(organizationId, person.Id, cancellationToken);
                foreach (var reservation in personReservations)
                {
                    if (reservation.Status == EquipmentReservationStatus.PendingApproval)
                    {
                        reservation.Reject(now, _currentUser.Subject, "Offboarding");
                        _activity.Add(new ActivityLog(organizationId, "reservation.rejected", "equipment_reservation", reservation.Id, _currentUser.Subject, "Offboarding", now));
                    }
                    else if (reservation.Status == EquipmentReservationStatus.Approved && reservation.StartAt > now)
                    {
                        reservation.Cancel(now, _currentUser.Subject, "Offboarding");
                        _activity.Add(new ActivityLog(organizationId, "reservation.cancelled", "equipment_reservation", reservation.Id, _currentUser.Subject, "Offboarding", now));
                    }
                }
            }

            // Spec 4.5 krok 8: wiadomość i publiczny token powstają tylko, gdy osoba ma e-mail i admin nie wyłączył
            // powiadomienia przy starcie. Brak adresu nie blokuje uruchomienia sprawy.
            PendingOffboardingEmail? pendingEmail = null;
            if (request.NotifyEmployee && !string.IsNullOrWhiteSpace(person.Email))
                pendingEmail = await IssueTokenAsync(offboardingCase, person, cancellationToken);

            if (pendingEmail is not null && _emailOutbox is not null)
            {
                // Token hash, workflow state and encrypted outbox payload commit atomically. SMTP is performed
                // later by the durable dispatcher, outside this transaction.
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await QueueIssuedLinkAsync(offboardingCase, person, pendingEmail, now, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    return true;
                }, cancellationToken);
            }
            else
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (pendingEmail is not null)
                {
                    await DeliverIssuedLinkAsync(offboardingCase, person, pendingEmail, now, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            return await _responseBuilder.BuildDetailsAsync(offboardingCase.OrganizationId, offboardingCase.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Ręczny odpowiednik jednego przebiegu <see cref="Tenebit.Application.People.PersonOffboardingSchedulerService"/>
    /// dla pojedynczej sprawy - dezaktywuje osobę (jeśli termin już minął) i próbuje zwolnić zaplanowane
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
        return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
    }

    /// <summary>Fizyczne przyjęcie zwrotu pozycji AssetReturn (spec 4.5 krok 10). Stosuje politykę zwrotu kategorii
    /// aktywa - DirectToStock i InspectionRequired reużywają <see cref="AssetReturnDispositionService"/> (ta sama
    /// logika co w AssignmentService). ReturnToVendor/Dispose są tu uproszczone do bezpośredniej zmiany statusu
    /// aktywa - pełny przepływ przekazania do zewnętrznego odbiorcy to osobny, przyszły temat (YAGNI na razie).</summary>
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
                    // Kontrola trwa - pozycja czeka na complete-inspection, nie ma jeszcze stanu końcowego.
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
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Kończy kontrolę powiązaną z pozycją InspectionRequired - reużywa <see cref="AssetInspectionService.CompleteAsync"/>
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
        if (item.IsResolved) return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
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
                    _ => OffboardingItemStatus.Retained // Retired/Disposed - aktywo fizycznie obecne, ale nie wraca do obiegu.
                };
                item.Resolve(status, request.Notes ?? request.DamageAssessmentNotes ?? "Wynik kontroli", _currentUser.Subject, now);
            }

            _activity.Add(new ActivityLog(organizationId, "offboarding.asset_inspection_completed", "offboarding_item", item.Id, _currentUser.Subject, item.Label, now));

            var items = await _items.ListByCaseAsync(organizationId, id, cancellationToken);
            offboardingCase.RecomputeStatus(items, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Ręczne zwolnienie miejsca licencyjnego dla pozycji LicenseRelease - dla trybu Manual albo gdy
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

        if (item.IsResolved) return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);

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
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Jawne rozstrzygnięcie Missing/Damaged/Retained dla pozycji AssetReturn - moment, w którym operator
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
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
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
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
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
            var alreadyCompleted = offboardingCase.Status == OffboardingCaseStatus.Completed;
            var protocolNumber = offboardingCase.FinalProtocolNumber ?? ReferenceNumberGenerator.Create("OFF", now);
            offboardingCase.Complete(now, _currentUser.Subject, protocolNumber);

            // Complete(...) jest no-op na już zakończonej sprawie (domena) - analogicznie nie duplikujemy wpisu
            // ActivityLog przy powtórnym wywołaniu, żeby dziennik pozostał idempotentny (spec 4.9/4.12).
            if (!alreadyCompleted)
            {
                _activity.Add(new ActivityLog(organizationId, "offboarding.completed", "offboarding_case", offboardingCase.Id, _currentUser.Subject, protocolNumber, now));
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Anuluje sprawę i - dla nieodebranych pozycji AssetReturn - przywraca aktywo do statusu Assigned
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
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
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
            return await _responseBuilder.BuildDetailsAsync(_currentUser.OrganizationId, id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<OffboardingCaseDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    // --- Publiczny kanał pracownika (spec 4.6) ---

    private sealed record PendingOffboardingEmail(string Link, string Subject, string Html, string TokenHash);

    private async Task<PendingOffboardingEmail> IssueTokenAsync(OffboardingCase offboardingCase, Domain.People.Person person, CancellationToken cancellationToken)
    {
        var generated = PublicTokenService.Generate();
        offboardingCase.SetPublicToken(generated.TokenHash, offboardingCase.ReturnDueDate.AddDays(30));
        var link = _linkBuilder.BuildOffboardingLink(generated.RawToken);
        var organization = await _organizations.GetAsync(offboardingCase.OrganizationId, cancellationToken);
        var (subject, html) = EmailTemplates.OffboardingLink(organization?.Language, person.FirstName, offboardingCase.ReturnDueDate, link);
        return new PendingOffboardingEmail(link, subject, html, generated.TokenHash);
    }

    private async Task QueueIssuedLinkAsync(OffboardingCase offboardingCase, Domain.People.Person person, PendingOffboardingEmail message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _emailOutbox!.EnqueueAsync(
            offboardingCase.OrganizationId,
            person.Email,
            message.Subject,
            message.Html,
            "offboarding-public-link",
            $"offboarding:{offboardingCase.Id:N}:{message.TokenHash}",
            cancellationToken);
        _activity.Add(new ActivityLog(offboardingCase.OrganizationId, "offboarding.link_queued", "offboarding_case", offboardingCase.Id, _currentUser.Subject, person.FullName, now));
    }

    private async Task DeliverIssuedLinkAsync(OffboardingCase offboardingCase, Domain.People.Person person, PendingOffboardingEmail message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await _emailSender.SendAsync(person.Email, message.Subject, message.Html, cancellationToken);
            _activity.Add(new ActivityLog(offboardingCase.OrganizationId, "offboarding.link_sent", "offboarding_case", offboardingCase.Id, _currentUser.Subject, person.FullName, now));
        }
        catch (Exception)
        {
            _activity.Add(new ActivityLog(offboardingCase.OrganizationId, "offboarding.email_failed", "offboarding_case", offboardingCase.Id, _currentUser.Subject, "delivery_failed", now));
        }
    }

    public async Task<Result<bool>> ResendLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<bool>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<bool>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var person = await _people.GetAsync(organizationId, offboardingCase.PersonId, cancellationToken);
        if (person is null || string.IsNullOrWhiteSpace(person.Email))
        {
            return Result<bool>.Failure(Error.Validation("Osoba nie ma adresu e-mail - nie można wysłać linku."));
        }

        // PublicTokenService przechowuje wyłącznie hash (surowy token nigdy nie jest zapisywany) - dlatego
        // "ponowne wysłanie z niezmienionym tokenem" nie jest technicznie odtwarzalne. Praktycznym i bezpiecznym
        // odpowiednikiem jest wystawienie nowego tokenu (identyczny efekt końcowy: pracownik dostaje działający
        // link), niezależnie czy poprzedni token istniał, wygasł, czy nigdy nie został wygenerowany.
        var now = _clock.UtcNow;
        var pendingEmail = await IssueTokenAsync(offboardingCase, person, cancellationToken);
        if (_emailOutbox is not null)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await QueueIssuedLinkAsync(offboardingCase, person, pendingEmail, now, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        else
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await DeliverIssuedLinkAsync(offboardingCase, person, pendingEmail, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return Result<bool>.Success(true);
    }

    public async Task<Result<string>> RegenerateLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.OffboardingManagers);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<string>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        var person = await _people.GetAsync(organizationId, offboardingCase.PersonId, cancellationToken);
        if (person is null || string.IsNullOrWhiteSpace(person.Email))
        {
            return Result<string>.Failure(Error.Validation("Osoba nie ma adresu e-mail - nie można wysłać linku."));
        }

        var now = _clock.UtcNow;
        var pendingEmail = await IssueTokenAsync(offboardingCase, person, cancellationToken);
        _activity.Add(new ActivityLog(organizationId, "offboarding.link_regenerated", "offboarding_case", offboardingCase.Id, _currentUser.Subject, person.FullName, now));
        if (_emailOutbox is not null)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await QueueIssuedLinkAsync(offboardingCase, person, pendingEmail, now, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        else
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await DeliverIssuedLinkAsync(offboardingCase, person, pendingEmail, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return Result<string>.Success(pendingEmail.Link);
    }

    /// <summary>Wspólna weryfikacja tokenu dla wszystkich publicznych endpointów offboardingu. Zwraca zawsze ten sam
    /// generyczny NotFound (bez ujawniania czy sprawa/organizacja istnieje) dla tokenu nieistniejącego, wygasłego
    /// lub unieważnionego - spec sekcja 13.</summary>
    private async Task<Result<OffboardingCase>> ResolveByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var candidate = await _cases.FindByPublicTokenHashAsync(TokenHasher.Hash(token), cancellationToken);
        // Cancel/RestoreEmployment/Complete revoke the token atomically, but every public command still
        // rechecks parent state here directly - a second, independent gate against a terminal case, not
        // just against token validity (audyt AUD3-008: linki publiczne pozostawały użyteczne po
        // anulowaniu lub zakończeniu procesu).
        if (candidate is not null
            && candidate.Status is not (OffboardingCaseStatus.Cancelled or OffboardingCaseStatus.Completed)
            && PublicTokenService.Verify(token, candidate.PublicTokenHash, candidate.PublicTokenExpiresAt ?? DateTimeOffset.MinValue, candidate.PublicTokenRevokedAt, now))
        {
            return Result<OffboardingCase>.Success(candidate);
        }

        SecurityTelemetry.PublicTokenRejected();
        return Result<OffboardingCase>.Failure(Error.NotFound("Link jest nieprawidłowy lub wygasł."));
    }

    public async Task<Result<PublicOffboardingResponse>> GetPublicAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<PublicOffboardingResponse>.Failure(resolved.Error!);

        return Result<PublicOffboardingResponse>.Success(await _responseBuilder.BuildPublicResponseAsync(resolved.Value!, cancellationToken));
    }

    public async Task<Result<PublicOffboardingResponse>> RecordEmployeeResponsesAsync(string token, SubmitPublicOffboardingResponseRequest request, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<PublicOffboardingResponse>.Failure(resolved.Error!);

        var offboardingCase = resolved.Value!;
        var now = _clock.UtcNow;
        var items = await _items.ListByCaseAsync(offboardingCase.OrganizationId, offboardingCase.Id, cancellationToken);
        var itemsById = items.ToDictionary(x => x.Id);

        foreach (var answer in request.Answers)
        {
            if (!itemsById.TryGetValue(answer.ItemId, out var item)) continue;

            try
            {
                // Odpowiedź pracownika NIGDY nie zmienia statusu aktywa - wyłącznie zapisuje deklarację do
                // ręcznego zatwierdzenia przez operatora (spec 2.3/4.6). Pozycja już rozliczona (IsResolved)
                // rzuca wyjątek domenowy - pomijamy ją, żeby jedna nieaktualna odpowiedź nie blokowała reszty.
                item.RecordEmployeeResponse(answer.Response, answer.Comment);
                _activity.Add(new ActivityLog(offboardingCase.OrganizationId, "offboarding.employee_responded", "offboarding_item", item.Id, "employee", answer.Response, now));
            }
            catch (DomainException)
            {
                // Pozycja już rozliczona - ignorujemy tę odpowiedź, reszta żądania jest przetwarzana dalej.
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<PublicOffboardingResponse>.Success(await _responseBuilder.BuildPublicResponseAsync(offboardingCase, cancellationToken));
    }

    public async Task<Result<Guid>> UploadPublicEvidenceAsync(string token, Guid itemId, string fileName, string? declaredContentType, byte[] content, CancellationToken cancellationToken)
    {
        var resolved = await ResolveByTokenAsync(token, cancellationToken);
        if (resolved.IsFailure) return Result<Guid>.Failure(resolved.Error!);

        var offboardingCase = resolved.Value!;
        var item = await _items.GetAsync(offboardingCase.OrganizationId, offboardingCase.Id, itemId, cancellationToken);
        if (item is null || item.Type != OffboardingItemType.AssetReturn || item.AssetId is null)
        {
            return Result<Guid>.Failure(Error.NotFound("Pozycja nie istnieje."));
        }

        var uploadResult = await _evidenceService.UploadViaPublicTokenAsync(offboardingCase.OrganizationId, item.AssetId.Value, item.Id, fileName, declaredContentType, content, cancellationToken);
        if (uploadResult.IsFailure) return Result<Guid>.Failure(uploadResult.Error!);

        return Result<Guid>.Success(uploadResult.Value!.Id);
    }

}
