using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;

namespace Tenebit.Application.Reservations;

/// <summary>Reguły dostępności aktywów (spec 8.5), niezależne od reszty serwisu rezerwacji (który powstanie w
/// kolejnym zadaniu). Liczy orientacyjną liczbę dostępnych sztuk kategorii w danym terminie.</summary>
public sealed class AssetAvailabilityService
{
    // Statusy, w których wydanie nadal "trzyma" aktywo — akceptowane, przeterminowane albo częściowo zwrócone
    // (pozycja z tego wydania może dalej być nieoddana).
    private static readonly AssignmentStatus[] OpenAssignmentStatuses =
    [
        AssignmentStatus.AwaitingAcceptance,
        AssignmentStatus.Accepted,
        AssignmentStatus.Overdue,
        AssignmentStatus.PartiallyReturned
    ];

    private readonly IAssetRepository _assets;
    private readonly IAssignmentRepository _assignments;
    private readonly IEquipmentReservationRepository _reservations;

    public AssetAvailabilityService(IAssetRepository assets, IAssignmentRepository assignments, IEquipmentReservationRepository reservations)
    {
        _assets = assets;
        _assignments = assignments;
        _reservations = reservations;
    }

    public Task<int> CountAvailableAsync(Guid organizationId, Guid categoryId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        CountAvailableAsync(organizationId, categoryId, from, to, null, cancellationToken);

    public async Task<int> CountAvailableAsync(Guid organizationId, Guid categoryId, DateTimeOffset from, DateTimeOffset to, string? location, CancellationToken cancellationToken)
    {
        // Bazowy status dostępny to InStock — Damaged/Lost/Retired/Disposed/InService/PendingReturn/Assigned
        // (oraz pozostałe statusy) są z definicji wykluczone (spec 8.5).
        var candidates = (await _assets.ListAsync(organizationId, null, AssetStatus.InStock, null, cancellationToken))
            .Where(a => a.CategoryId == categoryId && a.IsReservable && a.AssignedPersonId is null);

        if (!string.IsNullOrWhiteSpace(location))
        {
            candidates = candidates.Where(a => string.Equals(a.Location, location, StringComparison.OrdinalIgnoreCase));
        }

        var candidateList = candidates.ToList();
        if (candidateList.Count == 0) return 0;

        var reservedAssetIds = await GetReservedAssetIdsAsync(organizationId, from, to, cancellationToken);
        var openAssignedAssetIds = await GetOpenAssignedAssetIdsAsync(organizationId, from, to, cancellationToken);

        return candidateList.Count(a => !reservedAssetIds.Contains(a.Id) && !openAssignedAssetIds.Contains(a.Id));
    }

    /// <summary>Sprawdza dostępność KONKRETNEGO aktywa w terminie — używane przy zatwierdzaniu/zamianie (spec 8.5),
    /// gdzie samo policzenie wolnych sztuk kategorii nie wystarcza, bo trzeba wykluczyć aktywa już zarezerwowane
    /// albo związane otwartym wydaniem.</summary>
    public async Task<bool> IsAssetAvailableAsync(Guid organizationId, Guid assetId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(organizationId, assetId, cancellationToken);
        if (asset is null || asset.Status != AssetStatus.InStock || !asset.IsReservable || asset.AssignedPersonId is not null)
        {
            return false;
        }

        var reservedAssetIds = await GetReservedAssetIdsAsync(organizationId, from, to, cancellationToken);
        if (reservedAssetIds.Contains(assetId)) return false;

        var openAssignedAssetIds = await GetOpenAssignedAssetIdsAsync(organizationId, from, to, cancellationToken);
        return !openAssignedAssetIds.Contains(assetId);
    }

    private async Task<HashSet<Guid>> GetReservedAssetIdsAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        (await _reservations.ListApprovedOverlappingAsync(organizationId, from, to, cancellationToken))
            .SelectMany(r => r.Items)
            .Where(i => i.AssetId.HasValue)
            .Select(i => i.AssetId!.Value)
            .ToHashSet();

    private async Task<HashSet<Guid>> GetOpenAssignedAssetIdsAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        (await _assignments.ListAsync(organizationId, cancellationToken))
            .Where(a => OpenAssignmentStatuses.Contains(a.Status) && Overlaps(a, from, to))
            .SelectMany(a => a.Assets.Where(x => x.ReturnResolution is null).Select(x => x.AssetId))
            .ToHashSet();

    // Wydanie bez terminu zwrotu blokuje aktywo bezterminowo (spec: "bez dokładnej daty końcowej chyba że
    // wydanie ma DueDate").
    private static bool Overlaps(Assignment assignment, DateTimeOffset from, DateTimeOffset to)
    {
        if (assignment.IssuedAt > to) return false;
        if (assignment.DueDate is null) return true;

        var end = new DateTimeOffset(assignment.DueDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        return end >= from;
    }
}
