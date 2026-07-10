using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;

namespace Tenebit.Application.Audit;

public sealed class ActivityLogService
{
    private readonly IActivityLogRepository _activity;
    private readonly IOrganizationUserRepository _users;
    private readonly ICurrentUser _currentUser;

    public ActivityLogService(IActivityLogRepository activity, IOrganizationUserRepository users, ICurrentUser currentUser)
    {
        _activity = activity;
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedActivityLogResponse>> ListAsync(int page, int pageSize, string? entityType, Guid? entityId, string? search, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Auditor, TenebitRoles.AssetOperator, TenebitRoles.Manager, TenebitRoles.Hr);
        if (access.IsFailure) return Result<PagedActivityLogResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var (items, total) = await _activity.ListPagedAsync(organizationId, page, pageSize, entityType, entityId, search, cancellationToken);
        var users = await _users.ListAsync(organizationId, cancellationToken);

        var mapped = items.Select(log => new ActivityLogEntryResponse(
            log.Id,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.Details,
            ResolveActorDisplay(log.ActorSubject, users),
            log.CreatedAt)).ToList();

        return Result<PagedActivityLogResponse>.Success(new PagedActivityLogResponse(mapped, total, page, pageSize));
    }

    private static string ResolveActorDisplay(string actorSubject, IReadOnlyList<Domain.Identity.OrganizationUser> users)
    {
        if (Guid.TryParse(actorSubject, out var userId))
        {
            var user = users.FirstOrDefault(u => u.Id == userId);
            if (user is not null) return user.DisplayName;
        }

        return actorSubject;
    }
}
