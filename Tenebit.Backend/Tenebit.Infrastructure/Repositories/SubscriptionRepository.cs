using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Subscriptions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

internal sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly TenebitDbContext _context;

    public SubscriptionRepository(TenebitDbContext context)
    {
        _context = context;
    }

    public async Task<OrganizationSubscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await _context.Subscriptions
            .Where(s => s.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OrganizationSubscription?> GetByStripeCustomerAsync(string stripeCustomerId, CancellationToken cancellationToken)
    {
        return await _context.Subscriptions
            .Where(s => s.StripeCustomerId == stripeCustomerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(OrganizationSubscription subscription)
    {
        _context.Subscriptions.Add(subscription);
    }
}
