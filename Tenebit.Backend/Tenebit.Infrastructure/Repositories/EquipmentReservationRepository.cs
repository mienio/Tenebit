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

    public Task<EquipmentReservation?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.EquipmentReservations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public void Add(EquipmentReservation reservation) => _db.EquipmentReservations.Add(reservation);
}
