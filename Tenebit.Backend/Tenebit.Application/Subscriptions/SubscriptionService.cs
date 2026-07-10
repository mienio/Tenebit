using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Subscriptions;

public sealed class SubscriptionService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IAssetRepository _assets;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SubscriptionService(
        ISubscriptionRepository subscriptions,
        IAssetRepository assets,
        IActivityLogRepository activity,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _subscriptions = subscriptions;
        _assets = assets;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubscriptionResponse>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);

        if (subscription is null)
        {
            // Create default Free subscription
            subscription = new OrganizationSubscription(_currentUser.OrganizationId, SubscriptionPlan.Free.Key);
            _subscriptions.Add(subscription);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var assets = await _assets.ListAsync(_currentUser.OrganizationId, null, null, null, cancellationToken);
        var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;

        return Result<SubscriptionResponse>.Success(new SubscriptionResponse(
            subscription.Id,
            subscription.PlanKey,
            plan.Name,
            plan.AssetLimit,
            plan.MonthlyPrice,
            plan.Currency,
            assets.Count,
            subscription.Status.ToString(),
            subscription.CurrentPeriodEnd
        ));
    }

    public async Task<Result<SubscriptionResponse>> UpgradeAsync(string planKey, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<SubscriptionResponse>.Failure(access.Error!);

        var newPlan = SubscriptionPlan.FromKey(planKey);
        if (newPlan is null)
        {
            return Result<SubscriptionResponse>.Failure(Error.Validation($"Unknown plan: {planKey}"));
        }

        try
        {
            var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);

            if (subscription is null)
            {
                subscription = new OrganizationSubscription(_currentUser.OrganizationId, planKey);
                _subscriptions.Add(subscription);
            }
            else
            {
                subscription.Upgrade(planKey);
            }

            _activity.Add(new ActivityLog(
                _currentUser.OrganizationId,
                "subscription.upgraded",
                "subscription",
                subscription.Id,
                _currentUser.Subject,
                $"Upgraded to {newPlan.Name}",
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var assets = await _assets.ListAsync(_currentUser.OrganizationId, null, null, null, cancellationToken);

            return Result<SubscriptionResponse>.Success(new SubscriptionResponse(
                subscription.Id,
                subscription.PlanKey,
                newPlan.Name,
                newPlan.AssetLimit,
                newPlan.MonthlyPrice,
                newPlan.Currency,
                assets.Count,
                subscription.Status.ToString(),
                subscription.CurrentPeriodEnd
            ));
        }
        catch (DomainException ex)
        {
            return Result<SubscriptionResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<bool>> CanAddAssetAsync(CancellationToken cancellationToken)
    {
        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);

        if (subscription is null)
        {
            // No subscription = Free plan
            subscription = new OrganizationSubscription(_currentUser.OrganizationId, SubscriptionPlan.Free.Key);
            _subscriptions.Add(subscription);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var assets = await _assets.ListAsync(_currentUser.OrganizationId, null, null, null, cancellationToken);
        var limit = subscription.GetAssetLimit();

        return Result<bool>.Success(assets.Count < limit);
    }
}

public sealed record SubscriptionResponse(
    Guid Id,
    string PlanKey,
    string PlanName,
    int AssetLimit,
    decimal MonthlyPrice,
    string Currency,
    int CurrentAssetCount,
    string Status,
    DateTimeOffset CurrentPeriodEnd
);
