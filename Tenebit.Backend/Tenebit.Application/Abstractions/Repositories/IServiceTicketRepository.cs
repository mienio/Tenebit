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

public interface IServiceTicketRepository
{
    Task<ServiceTicket?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceTicket>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ServiceTicket> Items, int Total)> ListPagedAsync(Guid organizationId, ServiceTicketStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ServiceTicket> Items, int Total)> ListPagedScopedAsync(Guid organizationId, ServiceTicketStatus? status, int page, int pageSize, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken);
    void Add(ServiceTicket ticket);
}
