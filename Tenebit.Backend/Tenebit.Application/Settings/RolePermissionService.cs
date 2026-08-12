using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Settings;

namespace Tenebit.Application.Settings;

public sealed class RolePermissionService
{
    private readonly IRolePermissionRepository _rolePermissions;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RolePermissionService(IRolePermissionRepository rolePermissions, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _rolePermissions = rolePermissions;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<RolePermissionResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<IReadOnlyList<RolePermissionResponse>>.Failure(access.Error!);

        var overrides = await _rolePermissions.ListAsync(_currentUser.OrganizationId, cancellationToken);
        var result = new List<RolePermissionResponse>();
        foreach (var permission in RolePermissionKeys.All)
        {
            var defaults = RolePermissionKeys.DefaultAllowedRoles[permission.Key];
            foreach (var role in TenebitRoles.All)
            {
                var overrideRow = overrides.FirstOrDefault(x => x.RoleKey == role.Key && x.PermissionKey == permission.Key);
                var allowed = overrideRow?.Allowed ?? defaults.Contains(role.Key, StringComparer.OrdinalIgnoreCase);
                result.Add(new RolePermissionResponse(role.Key, role.Label, permission.Key, permission.Label, permission.Description, allowed));
            }
        }

        return Result<IReadOnlyList<RolePermissionResponse>>.Success(result);
    }

    public async Task<Result> SetAsync(SetRolePermissionRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return access;

        if (TenebitRoles.All.All(x => x.Key != request.RoleKey)) return Result.Failure(Error.Validation($"Nieznana rola: {request.RoleKey}."));
        if (RolePermissionKeys.All.All(x => x.Key != request.PermissionKey)) return Result.Failure(Error.Validation($"Nieznane uprawnienie: {request.PermissionKey}."));

        var organizationId = _currentUser.OrganizationId;
        var existing = await _rolePermissions.FindAsync(organizationId, request.RoleKey, request.PermissionKey, cancellationToken);
        if (existing is null)
        {
            _rolePermissions.Add(new RolePermission(organizationId, request.RoleKey, request.PermissionKey, request.Allowed));
        }
        else
        {
            existing.SetAllowed(request.Allowed);
        }

        _activity.Add(new ActivityLog(organizationId, "role_permission.updated", "role_permission", Guid.NewGuid(), _currentUser.Subject, $"{request.RoleKey}: {request.PermissionKey} = {request.Allowed}", _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
