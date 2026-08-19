using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Dashboards;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Identity;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Reservations;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IEquipmentReservationRepository
{
    Task<IReadOnlyList<EquipmentReservation>> ListOpenAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipmentReservation>> ListApprovedOverlappingAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipmentReservation>> ListByRequesterAsync(Guid organizationId, Guid requesterPersonId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<EquipmentReservation> Items, int Total)> ListPagedAsync(Guid organizationId, EquipmentReservationStatus? status, IReadOnlyCollection<Guid>? requesterPersonIds, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipmentReservation>> ListForCalendarAsync(Guid organizationId, DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<Guid>? requesterPersonIds, CancellationToken cancellationToken);
    Task<EquipmentReservation?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<EquipmentReservation?> GetByAssignmentIdAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken);
    void Add(EquipmentReservation reservation);
}
