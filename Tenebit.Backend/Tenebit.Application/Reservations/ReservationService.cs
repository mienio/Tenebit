using Tenebit.Application.Abstractions;
using Tenebit.Application.Assignments;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Reservations;

namespace Tenebit.Application.Reservations;

/// <summary>Wniosek pracownika (create/submit/cancel), zatwierdzanie/odrzucanie/zamiana przez administratora
/// (spec 8.6–8.9), wydanie sprzętu (checkout, spec 8.8) oraz widok kalendarzowy (spec 8.7), z re-weryfikacją
/// dostępności przy zatwierdzeniu i wydaniu (spec 8.5/8.12).</summary>
public sealed class ReservationService
{
    private readonly IEquipmentReservationRepository _reservations;
    private readonly IEquipmentKitDefinitionRepository _kits;
    private readonly IAssetRepository _assets;
    private readonly IPersonRepository _people;
    private readonly IActivityLogRepository _activity;
    private readonly AssetAvailabilityService _availability;
    private readonly AssignmentService _assignmentService;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ReservationService(IEquipmentReservationRepository reservations, IEquipmentKitDefinitionRepository kits,
        IAssetRepository assets, IPersonRepository people, IActivityLogRepository activity,
        AssetAvailabilityService availability, AssignmentService assignmentService, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _reservations = reservations;
        _kits = kits;
        _assets = assets;
        _people = people;
        _activity = activity;
        _availability = availability;
        _assignmentService = assignmentService;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    // --- Strona pracownika (/api/my/reservations) ---

    public async Task<Result<ReservationDetailsResponse>> CreateAsync(CreateReservationRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser);
        if (access.IsFailure) return Result<ReservationDetailsResponse>.Failure(access.Error!);

        var person = await GetRequesterPersonAsync(cancellationToken);
        if (person is null) return Result<ReservationDetailsResponse>.Failure(Error.Validation("Konto nie jest powiązane z osobą — skontaktuj się z administratorem."));
        if (!person.CanReceiveNewObligations) return Result<ReservationDetailsResponse>.Failure(Error.Validation("Nie można złożyć wniosku — osoba nie jest aktywna."));

        try
        {
            var now = _clock.UtcNow;
            var reservation = new EquipmentReservation(_currentUser.OrganizationId, person.Id, request.StartAt, request.EndAt, request.Purpose, request.PickupLocation, request.Notes);
            foreach (var (categoryId, quantity, kitDefinitionId) in await ExpandItemsAsync(reservation.OrganizationId, request.Items, cancellationToken))
            {
                reservation.AddItem(categoryId, quantity, kitDefinitionId);
            }

            _reservations.Add(reservation);
            _activity.Add(new ActivityLog(reservation.OrganizationId, "reservation.created", "equipment_reservation", reservation.Id, _currentUser.Subject, reservation.Purpose, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<ReservationResponse>>> ListMyAsync(CancellationToken cancellationToken)
    {
        var person = await GetRequesterPersonAsync(cancellationToken);
        if (person is null) return Result<IReadOnlyList<ReservationResponse>>.Success([]);

        var reservations = await _reservations.ListByRequesterAsync(_currentUser.OrganizationId, person.Id, cancellationToken);
        return Result<IReadOnlyList<ReservationResponse>>.Success(reservations.Select(Map).ToList());
    }

    public async Task<Result<ReservationDetailsResponse>> GetMyAsync(Guid id, CancellationToken cancellationToken)
    {
        var person = await GetRequesterPersonAsync(cancellationToken);
        if (person is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        var reservation = await _reservations.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (reservation is null || reservation.RequesterPersonId != person.Id)
            return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
    }

    public async Task<Result<ReservationDetailsResponse>> UpdateMyAsync(Guid id, UpdateReservationRequest request, CancellationToken cancellationToken)
    {
        var person = await GetRequesterPersonAsync(cancellationToken);
        if (person is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        var reservation = await _reservations.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (reservation is null || reservation.RequesterPersonId != person.Id)
            return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        try
        {
            reservation.UpdateDraft(request.StartAt, request.EndAt, request.Purpose, request.PickupLocation, request.Notes);
            reservation.ReplaceItems(await ExpandItemsAsync(reservation.OrganizationId, request.Items, cancellationToken));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ReservationDetailsResponse>> SubmitMyAsync(Guid id, CancellationToken cancellationToken)
    {
        var person = await GetRequesterPersonAsync(cancellationToken);
        if (person is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        var reservation = await _reservations.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (reservation is null || reservation.RequesterPersonId != person.Id)
            return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            reservation.Submit(now);
            _activity.Add(new ActivityLog(reservation.OrganizationId, "reservation.submitted", "equipment_reservation", reservation.Id, _currentUser.Subject, reservation.Purpose, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ReservationDetailsResponse>> CancelMyAsync(Guid id, CancelReservationRequest request, CancellationToken cancellationToken)
    {
        var person = await GetRequesterPersonAsync(cancellationToken);
        if (person is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        var reservation = await _reservations.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (reservation is null || reservation.RequesterPersonId != person.Id)
            return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            reservation.Cancel(now, _currentUser.Subject, request.Reason);
            _activity.Add(new ActivityLog(reservation.OrganizationId, "reservation.cancelled", "equipment_reservation", reservation.Id, _currentUser.Subject, request.Reason, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    // --- Strona zatwierdzającego (/api/reservations) ---

    public async Task<Result<PagedResult<ReservationResponse>>> ListPagedAsync(EquipmentReservationStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Manager);
        if (access.IsFailure) return Result<PagedResult<ReservationResponse>>.Failure(access.Error!);

        var requesterPersonIds = await ResolveManagerSubordinateFilterAsync(cancellationToken);
        var (reservations, total) = await _reservations.ListPagedAsync(_currentUser.OrganizationId, status, requesterPersonIds, page, pageSize, cancellationToken);
        return Result<PagedResult<ReservationResponse>>.Success(new PagedResult<ReservationResponse>(reservations.Select(Map).ToList(), total, page, pageSize));
    }

    public async Task<Result<ReservationDetailsResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Manager);
        if (access.IsFailure) return Result<ReservationDetailsResponse>.Failure(access.Error!);

        var reservation = await _reservations.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (reservation is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        if (!_currentUser.HasAnyRole(TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator))
        {
            var managerAccess = await EnsureApproveAccessAsync(reservation, cancellationToken);
            if (managerAccess.IsFailure) return Result<ReservationDetailsResponse>.Failure(managerAccess.Error!);
        }

        return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
    }

    public async Task<Result<ReservationDetailsResponse>> ApproveAsync(Guid id, ApproveReservationRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return Result<ReservationDetailsResponse>.Failure(Error.Unauthorized());

        var organizationId = _currentUser.OrganizationId;
        var reservation = await _reservations.GetAsync(organizationId, id, cancellationToken);
        if (reservation is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        var access = await EnsureApproveAccessAsync(reservation, cancellationToken);
        if (access.IsFailure) return Result<ReservationDetailsResponse>.Failure(access.Error!);

        if (reservation.Status != EquipmentReservationStatus.PendingApproval)
            return Result<ReservationDetailsResponse>.Failure(Error.Validation("Zatwierdzić można tylko wniosek oczekujący na akceptację."));

        var itemsById = reservation.Items.ToDictionary(x => x.Id);
        var allocations = request.Allocations ?? [];

        if (allocations.Count != itemsById.Count || allocations.Any(a => !itemsById.ContainsKey(a.ItemId)))
            return Result<ReservationDetailsResponse>.Failure(Error.Validation("Każda pozycja wniosku musi mieć przydzielone dokładnie jedno aktywo."));

        if (allocations.Select(a => a.AssetId).Distinct().Count() != allocations.Count)
            return Result<ReservationDetailsResponse>.Failure(Error.Validation("To samo aktywo nie może być przydzielone do dwóch pozycji."));

        // Re-weryfikacja dostępności KONKRETNYCH aktywów (spec 8.5/8.12) — all-or-nothing.
        var unavailableItems = new List<Guid>();
        foreach (var allocation in allocations)
        {
            var item = itemsById[allocation.ItemId];
            var asset = await _assets.GetAsync(organizationId, allocation.AssetId, cancellationToken);
            if (asset is null || asset.CategoryId != item.RequestedCategoryId)
            {
                unavailableItems.Add(allocation.ItemId);
                continue;
            }

            if (!await _availability.IsAssetAvailableAsync(organizationId, allocation.AssetId, reservation.StartAt, reservation.EndAt, cancellationToken))
            {
                unavailableItems.Add(allocation.ItemId);
            }
        }

        if (unavailableItems.Count > 0)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Conflict($"Następujące pozycje nie są dostępne: {string.Join(", ", unavailableItems)}"));
        }

        try
        {
            var now = _clock.UtcNow;
            reservation.Approve(now, _currentUser.Subject);
            foreach (var allocation in allocations)
            {
                itemsById[allocation.ItemId].Allocate(allocation.AssetId);
            }

            _activity.Add(new ActivityLog(organizationId, "reservation.approved", "equipment_reservation", reservation.Id, _currentUser.Subject, reservation.Purpose, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (ConcurrencyException)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Conflict("Wniosek został zmodyfikowany równolegle — odśwież i spróbuj ponownie."));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ReservationDetailsResponse>> RejectAsync(Guid id, RejectReservationRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return Result<ReservationDetailsResponse>.Failure(Error.Unauthorized());

        var organizationId = _currentUser.OrganizationId;
        var reservation = await _reservations.GetAsync(organizationId, id, cancellationToken);
        if (reservation is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        var access = await EnsureApproveAccessAsync(reservation, cancellationToken);
        if (access.IsFailure) return Result<ReservationDetailsResponse>.Failure(access.Error!);

        try
        {
            var now = _clock.UtcNow;
            reservation.Reject(now, _currentUser.Subject, request.Reason);
            _activity.Add(new ActivityLog(organizationId, "reservation.rejected", "equipment_reservation", reservation.Id, _currentUser.Subject, request.Reason, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ReservationDetailsResponse>> SubstituteAsync(Guid id, SubstituteReservationItemRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return Result<ReservationDetailsResponse>.Failure(Error.Unauthorized());

        var organizationId = _currentUser.OrganizationId;
        var reservation = await _reservations.GetAsync(organizationId, id, cancellationToken);
        if (reservation is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        var access = await EnsureApproveAccessAsync(reservation, cancellationToken);
        if (access.IsFailure) return Result<ReservationDetailsResponse>.Failure(access.Error!);

        var item = reservation.Items.FirstOrDefault(x => x.Id == request.ItemId);
        if (item is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Pozycja nie istnieje."));

        var newAsset = await _assets.GetAsync(organizationId, request.NewAssetId, cancellationToken);
        if (newAsset is null || newAsset.CategoryId != item.RequestedCategoryId)
            return Result<ReservationDetailsResponse>.Failure(Error.Validation("Nowe aktywo musi należeć do tej samej kategorii co pozycja."));

        if (!await _availability.IsAssetAvailableAsync(organizationId, request.NewAssetId, reservation.StartAt, reservation.EndAt, cancellationToken))
            return Result<ReservationDetailsResponse>.Failure(Error.Conflict("Wskazane aktywo nie jest dostępne w tym terminie."));

        try
        {
            item.Substitute(request.NewAssetId, request.Reason);
            _activity.Add(new ActivityLog(organizationId, "reservation.asset_substituted", "equipment_reservation_item", item.Id, _currentUser.Subject, request.Reason, _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ReservationDetailsResponse>> CancelAsync(Guid id, CancelReservationRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return Result<ReservationDetailsResponse>.Failure(Error.Unauthorized());

        var organizationId = _currentUser.OrganizationId;
        var reservation = await _reservations.GetAsync(organizationId, id, cancellationToken);
        if (reservation is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        var access = await EnsureApproveAccessAsync(reservation, cancellationToken);
        if (access.IsFailure) return Result<ReservationDetailsResponse>.Failure(access.Error!);

        try
        {
            var now = _clock.UtcNow;
            reservation.Cancel(now, _currentUser.Subject, request.Reason);
            _activity.Add(new ActivityLog(organizationId, "reservation.cancelled", "equipment_reservation", reservation.Id, _currentUser.Subject, request.Reason, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<ReservationDetailsResponse>> CheckoutAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.ReservationCheckoutRoles);
        if (access.IsFailure) return Result<ReservationDetailsResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var reservation = await _reservations.GetAsync(organizationId, id, cancellationToken);
        if (reservation is null) return Result<ReservationDetailsResponse>.Failure(Error.NotFound("Wniosek nie istnieje."));

        if (reservation.Status != EquipmentReservationStatus.Approved && reservation.Status != EquipmentReservationStatus.ReadyForPickup)
            return Result<ReservationDetailsResponse>.Failure(Error.Validation("Wydać sprzęt można tylko dla wniosku zatwierdzonego lub gotowego do odbioru."));

        if (reservation.Items.Any(x => x.AssetId is null))
            return Result<ReservationDetailsResponse>.Failure(Error.Validation("Wszystkie pozycje wniosku muszą mieć przydzielone aktywo przed wydaniem."));

        // Re-weryfikacja dostępności KAŻDEGO przydzielonego aktywa tuż przed wydaniem (spec 8.8) — mogło
        // zostać w międzyczasie oznaczone jako niedostępne (np. Damaged) — excludeReservationId pomija
        // konflikt z własną (już zaakceptowaną) rezerwacją.
        var unavailable = new List<Guid>();
        foreach (var item in reservation.Items)
        {
            if (!await _availability.IsAssetAvailableAsync(organizationId, item.AssetId!.Value, reservation.StartAt, reservation.EndAt, cancellationToken, reservation.Id))
            {
                unavailable.Add(item.Id);
            }
        }

        if (unavailable.Count > 0)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Conflict($"Następujące pozycje przestały być dostępne: {string.Join(", ", unavailable)}"));
        }

        var createRequest = new CreateAssignmentRequest(
            reservation.RequesterPersonId,
            reservation.Items.Select(x => new AssignmentAssetRequest(x.AssetId!.Value, null)).ToList(),
            [],
            DateOnly.FromDateTime(reservation.EndAt.UtcDateTime),
            reservation.Notes);

        var assignmentResult = await _assignmentService.CreateAsync(createRequest, cancellationToken);
        if (assignmentResult.IsFailure) return Result<ReservationDetailsResponse>.Failure(assignmentResult.Error!);

        try
        {
            var now = _clock.UtcNow;
            reservation.MarkCheckedOut(assignmentResult.Value!.Id, now);
            _activity.Add(new ActivityLog(organizationId, "reservation.checked_out", "equipment_reservation", reservation.Id, _currentUser.Subject, reservation.Purpose, now));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReservationDetailsResponse>.Success(MapDetails(reservation));
        }
        catch (DomainException ex)
        {
            return Result<ReservationDetailsResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<ReservationCalendarItemResponse>>> GetCalendarAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Manager);
        if (access.IsFailure) return Result<IReadOnlyList<ReservationCalendarItemResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var requesterPersonIds = await ResolveManagerSubordinateFilterAsync(cancellationToken);
        var reservations = await _reservations.ListForCalendarAsync(organizationId, from, to, requesterPersonIds, cancellationToken);
        var now = _clock.UtcNow;
        var today = now.UtcDateTime.Date;

        // Konflikt = to samo aktywo pojawia się w co najmniej dwóch rezerwacjach z nachodzącymi się przedziałami.
        var assetOccurrences = reservations
            .SelectMany(r => r.Items.Where(i => i.AssetId.HasValue).Select(i => (Reservation: r, AssetId: i.AssetId!.Value)))
            .GroupBy(x => x.AssetId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Reservation).ToList());

        bool IsConflicting(EquipmentReservation reservation)
        {
            var assetIds = reservation.Items.Where(i => i.AssetId.HasValue).Select(i => i.AssetId!.Value);
            foreach (var assetId in assetIds)
            {
                var others = assetOccurrences[assetId].Where(other => other.Id != reservation.Id);
                if (others.Any(other => other.StartAt < reservation.EndAt && other.EndAt > reservation.StartAt))
                {
                    return true;
                }
            }

            return false;
        }

        var result = reservations
            .Select(r => new ReservationCalendarItemResponse(
                r.Id,
                r.RequesterPersonId,
                r.Status,
                r.StartAt,
                r.EndAt,
                r.Purpose,
                r.PickupLocation,
                r.Items.Where(i => i.AssetId.HasValue).Select(i => i.AssetId!.Value).ToList(),
                IsConflicting(r),
                r.StartAt.UtcDateTime.Date == today,
                r.EndAt < now && r.Status == EquipmentReservationStatus.CheckedOut))
            .ToList();

        return Result<IReadOnlyList<ReservationCalendarItemResponse>>.Success(result);
    }

    /// <summary>Zwraca null (brak filtrowania — widok pełny) dla Owner/Admin/AssetOperator; dla samego
    /// Managera zwraca zbiór Id osób, których jest bezpośrednim przełożonym (spec 8.10), żeby lista/kalendarz
    /// pokazywały wyłącznie wnioski jego podwładnych — spójnie z <see cref="EnsureApproveAccessAsync"/>.</summary>
    private async Task<IReadOnlyCollection<Guid>?> ResolveManagerSubordinateFilterAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.HasAnyRole(TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator))
        {
            return null;
        }

        var currentPerson = await GetRequesterPersonAsync(cancellationToken);
        if (currentPerson is null) return [];

        var people = await _people.ListAsync(_currentUser.OrganizationId, search: null, cancellationToken);
        return people.Where(p => p.ManagerId == currentPerson.Id).Select(p => p.Id).ToList();
    }

    private async Task<Result> EnsureApproveAccessAsync(EquipmentReservation reservation, CancellationToken cancellationToken)
    {
        if (_currentUser.HasAnyRole(TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator))
        {
            return Result.Success();
        }

        if (_currentUser.HasAnyRole(TenebitRoles.Manager))
        {
            var currentPerson = await GetRequesterPersonAsync(cancellationToken);
            if (currentPerson is not null)
            {
                var requester = await _people.GetAsync(reservation.OrganizationId, reservation.RequesterPersonId, cancellationToken);
                if (requester?.ManagerId == currentPerson.Id)
                {
                    return Result.Success();
                }
            }

            return Result.Failure(Error.Forbidden("Kierownik może zatwierdzać wyłącznie wnioski swoich bezpośrednich podwładnych."));
        }

        return Result.Failure(Error.Forbidden());
    }

    private async Task<Person?> GetRequesterPersonAsync(CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        if (string.IsNullOrEmpty(_currentUser.Email)) return null;
        return await _people.FindByEmailAsync(organizationId, _currentUser.Email, cancellationToken);
    }

    /// <summary>Zamienia pozycje żądania na listę (kategoria, ilość, zestaw). Wybór zestawu rozwija się do jego
    /// kategorii składowych — zestaw sam w sobie nie przechowuje konkretnych AssetId (spec 8.3).</summary>
    private async Task<IReadOnlyList<(Guid CategoryId, int Quantity, Guid? KitDefinitionId)>> ExpandItemsAsync(Guid organizationId, IReadOnlyList<ReservationItemRequest> items, CancellationToken cancellationToken)
    {
        var result = new List<(Guid, int, Guid?)>();
        foreach (var item in items)
        {
            if (item.CategoryId.HasValue)
            {
                result.Add((item.CategoryId.Value, item.Quantity, null));
            }
            else if (item.KitDefinitionId.HasValue)
            {
                var kit = await _kits.GetAsync(organizationId, item.KitDefinitionId.Value, cancellationToken);
                if (kit is null) throw new DomainException("Wybrany zestaw nie istnieje.");
                foreach (var kitItem in kit.Items)
                {
                    result.Add((kitItem.AssetCategoryId, kitItem.RequiredQuantity * item.Quantity, kit.Id));
                }
            }
            else
            {
                throw new DomainException("Każda pozycja musi wskazywać kategorię albo zestaw.");
            }
        }

        return result;
    }

    private static ReservationResponse Map(EquipmentReservation reservation) => new(
        reservation.Id, reservation.RequesterPersonId, reservation.Status, reservation.StartAt, reservation.EndAt,
        reservation.Purpose, reservation.PickupLocation, reservation.Notes, reservation.RequestedAt, reservation.ApprovedAt,
        reservation.ApprovedBy, reservation.RejectedAt, reservation.RejectedBy, reservation.DecisionNotes,
        reservation.CancelledAt, reservation.CancelledBy, reservation.CancellationReason, reservation.CreatedAt);

    private static ReservationItemResponse MapItem(EquipmentReservationItem item) => new(
        item.Id, item.RequestedCategoryId, item.RequestedQuantity, item.KitDefinitionId, item.AssetId,
        item.OriginalAssetId, item.SubstitutionReason, item.Status);

    private static ReservationDetailsResponse MapDetails(EquipmentReservation reservation) =>
        new(Map(reservation), reservation.Items.Select(MapItem).ToList());
}
