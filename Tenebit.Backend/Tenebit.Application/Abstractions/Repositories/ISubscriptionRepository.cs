using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface ISubscriptionRepository
{
    Task<OrganizationSubscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<OrganizationSubscription?> GetByPaddleCustomerAsync(string paddleCustomerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationSubscription>> ListWithPaddleSubscriptionAsync(CancellationToken cancellationToken);

    /// <summary>Organizations that have started Paddle billing (have a customer) but never got a
    /// PaddleSubscriptionId linked - the case a lost/failed created-subscription webhook leaves behind,
    /// which <see cref="ListWithPaddleSubscriptionAsync"/> can never discover since it requires one.</summary>
    Task<IReadOnlyList<OrganizationSubscription>> ListPendingPaddleLinkAsync(CancellationToken cancellationToken);
    void Add(OrganizationSubscription subscription);
}
