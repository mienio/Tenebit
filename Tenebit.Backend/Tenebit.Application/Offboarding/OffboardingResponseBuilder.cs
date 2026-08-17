using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Common;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Reservations;
using Tenebit.Application.Reservations;

namespace Tenebit.Application.Offboarding;

/// <summary>Buduje response DTO dla spraw offboardingowych (widok wewnętrzny i publiczny kanał pracownika)
/// — wydzielone z OffboardingService (audyt P2 #3), żeby ta jedna odpowiedzialność miała jedno miejsce.</summary>
public sealed class OffboardingResponseBuilder
{
    private readonly IOffboardingCaseRepository _cases;
    private readonly IOffboardingItemRepository _items;
    private readonly IPersonRepository _people;
    private readonly IOrganizationRepository _organizations;
    private readonly IAssetRepository _assets;
    private readonly IAssetEvidenceRepository _evidence;
    private readonly IEquipmentReservationRepository _reservations;
    private readonly IClock _clock;

    public OffboardingResponseBuilder(IOffboardingCaseRepository cases, IOffboardingItemRepository items, IPersonRepository people,
        IOrganizationRepository organizations, IAssetRepository assets, IAssetEvidenceRepository evidence,
        IEquipmentReservationRepository reservations, IClock clock)
    {
        _cases = cases;
        _items = items;
        _people = people;
        _organizations = organizations;
        _assets = assets;
        _evidence = evidence;
        _reservations = reservations;
        _clock = clock;
    }

    public async Task<Result<OffboardingCaseDetailsResponse>> BuildDetailsAsync(Guid organizationId, Guid id, CancellationToken cancellationToken)
    {
        var offboardingCase = await _cases.GetAsync(organizationId, id, cancellationToken);
        if (offboardingCase is null) return Result<OffboardingCaseDetailsResponse>.Failure(Error.NotFound("Sprawa offboardingowa nie istnieje."));

        return Result<OffboardingCaseDetailsResponse>.Success(await BuildDetailsAsync(offboardingCase, cancellationToken));
    }

    public async Task<OffboardingCaseDetailsResponse> BuildDetailsAsync(OffboardingCase offboardingCase, CancellationToken cancellationToken)
    {
        var person = await _people.GetAsync(offboardingCase.OrganizationId, offboardingCase.PersonId, cancellationToken);
        var items = await _items.ListByCaseAsync(offboardingCase.OrganizationId, offboardingCase.Id, cancellationToken);
        var names = person is null ? new Dictionary<Guid, string>() : new Dictionary<Guid, string> { [person.Id] = person.FullName };
        var reservations = await ListRelevantReservationsAsync(offboardingCase.OrganizationId, offboardingCase.PersonId, cancellationToken);
        return new OffboardingCaseDetailsResponse(Map(offboardingCase, names), items.Select(MapItem).ToList(), reservations);
    }

    public async Task<PublicOffboardingResponse> BuildPublicResponseAsync(OffboardingCase offboardingCase, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(offboardingCase.OrganizationId, cancellationToken);
        var items = await _items.ListByCaseAsync(offboardingCase.OrganizationId, offboardingCase.Id, cancellationToken);
        var assetItems = items.Where(x => x.Type == OffboardingItemType.AssetReturn).ToList();

        var assetTags = new Dictionary<Guid, string>();
        var issuePhotos = new Dictionary<Guid, Guid>();
        foreach (var item in assetItems.Where(x => x.AssetId is not null))
        {
            var asset = await _assets.GetAsync(offboardingCase.OrganizationId, item.AssetId!.Value, cancellationToken);
            if (asset is not null) assetTags[item.Id] = asset.AssetTag;

            var evidence = (await _evidence.ListByAssetAsync(offboardingCase.OrganizationId, item.AssetId!.Value, cancellationToken))
                .Where(x => x.Phase == EvidencePhase.Issue)
                .OrderByDescending(x => x.UploadedAt)
                .FirstOrDefault();
            if (evidence is not null) issuePhotos[item.Id] = evidence.Id;
        }

        var itemResponses = assetItems.Select(item => new PublicOffboardingItemResponse(
            item.Id, item.Label, assetTags.GetValueOrDefault(item.Id), item.Status, item.EmployeeResponse, item.EmployeeComment,
            issuePhotos.GetValueOrDefault(item.Id))).ToList();

        return new PublicOffboardingResponse(organization?.Name ?? string.Empty, offboardingCase.ReturnDueDate,
            offboardingCase.DefaultReturnLocation, offboardingCase.Notes, itemResponses);
    }

    public async Task<IReadOnlyList<ReservationResponse>> ListRelevantReservationsAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        return (await _reservations.ListByRequesterAsync(organizationId, personId, cancellationToken))
            .Where(r => r.Status == EquipmentReservationStatus.PendingApproval
                || (r.Status is EquipmentReservationStatus.Approved or EquipmentReservationStatus.ReadyForPickup
                    or EquipmentReservationStatus.CheckedOut && r.EndAt > now))
            .Select(MapReservation)
            .ToList();
    }

    private static ReservationResponse MapReservation(EquipmentReservation reservation) => new(
        reservation.Id, reservation.RequesterPersonId, reservation.Status, reservation.StartAt, reservation.EndAt,
        reservation.Purpose, reservation.PickupLocation, reservation.Notes, reservation.RequestedAt, reservation.ApprovedAt,
        reservation.ApprovedBy, reservation.RejectedAt, reservation.RejectedBy, reservation.DecisionNotes,
        reservation.CancelledAt, reservation.CancelledBy, reservation.CancellationReason, reservation.CreatedAt);

    public static OffboardingCaseResponse Map(OffboardingCase offboardingCase, IReadOnlyDictionary<Guid, string> personNames) =>
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

    public static OffboardingItemResponse MapItem(OffboardingItem item) =>
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
