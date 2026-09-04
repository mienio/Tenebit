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

public interface ISubscriptionRepository
{
    Task<OrganizationSubscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<OrganizationSubscription?> GetByStripeCustomerAsync(string stripeCustomerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationSubscription>> ListWithStripeSubscriptionAsync(CancellationToken cancellationToken);

    /// <summary>Organizations that have started Stripe billing (have a customer) but never got a
    /// StripeSubscriptionId linked - the case a lost/failed created-subscription webhook leaves behind,
    /// which <see cref="ListWithStripeSubscriptionAsync"/> can never discover since it requires one.</summary>
    Task<IReadOnlyList<OrganizationSubscription>> ListPendingStripeLinkAsync(CancellationToken cancellationToken);
    void Add(OrganizationSubscription subscription);
}
