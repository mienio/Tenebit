using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Reservations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class EquipmentReservationRepository : IEquipmentReservationRepository
{
    // Statusy, w których rezerwacja nadal "trzyma" przydzielone aktywo (sekcja 8.5): zatwierdzona, gotowa do
    // odbioru albo już wydana, ale jeszcze nie rozliczona.
    private static readonly EquipmentReservationStatus[] HoldingStatuses =
    [
        EquipmentReservationStatus.Approved,
        EquipmentReservationStatus.ReadyForPickup,
        EquipmentReservationStatus.CheckedOut
    ];

    // Widok kalendarza (spec 8.7) pokazuje również oczekujące na akceptację, żeby administrator widział
    // nadchodzące konflikty zanim jeszcze zatwierdzi wniosek.
    private static readonly EquipmentReservationStatus[] CalendarStatuses =
    [
        EquipmentReservationStatus.PendingApproval,
        EquipmentReservationStatus.Approved,
        EquipmentReservationStatus.ReadyForPickup,
        EquipmentReservationStatus.CheckedOut
    ];

    private readonly TenebitDbContext _db;

    public EquipmentReservationRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<EquipmentReservation>> ListApprovedOverlappingAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await _db.EquipmentReservations
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OrganizationId == organizationId
                && HoldingStatuses.Contains(x.Status)
                && x.StartAt < to
                && x.EndAt > from)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EquipmentReservation>> ListForCalendarAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<Guid>? requesterPersonIds, CancellationToken cancellationToken) =>
        await _db.EquipmentReservations
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OrganizationId == organizationId
                && CalendarStatuses.Contains(x.Status)
                && x.StartAt < to
                && x.EndAt > from
                && (requesterPersonIds == null || requesterPersonIds.Contains(x.RequesterPersonId)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EquipmentReservation>> ListByRequesterAsync(Guid organizationId, Guid requesterPersonId, CancellationToken cancellationToken) =>
        await _db.EquipmentReservations
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OrganizationId == organizationId && x.RequesterPersonId == requesterPersonId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<EquipmentReservation> Items, int Total)> ListPagedAsync(Guid organizationId, EquipmentReservationStatus? status, IReadOnlyCollection<Guid>? requesterPersonIds, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.EquipmentReservations
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OrganizationId == organizationId
                && (!status.HasValue || x.Status == status.Value)
                && (requesterPersonIds == null || requesterPersonIds.Contains(x.RequesterPersonId)));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<EquipmentReservation?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.EquipmentReservations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<EquipmentReservation?> GetByAssignmentIdAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken) =>
        _db.EquipmentReservations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.AssignmentId == assignmentId, cancellationToken);

    public void Add(EquipmentReservation reservation) => _db.EquipmentReservations.Add(reservation);
}
