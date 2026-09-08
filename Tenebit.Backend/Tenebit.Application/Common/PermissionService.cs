using Tenebit.Application.Abstractions;

namespace Tenebit.Application.Common;

// Generic per-module permission check, backed by the same role_permissions override table as the
// single-field ViewLicenseKeys check used to be the only consumer of. Owner always passes, mirroring
// the Owner bypass every other permission check in this codebase already has.
public interface IPermissionService
{
    Task<bool> HasAsync(string module, string action, CancellationToken cancellationToken);
    Task<Result> EnsureAsync(string module, string action, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, ModulePermissions>> GetEffectivePermissionsAsync(CancellationToken cancellationToken);
}

public sealed record ModulePermissions(bool View, bool Manage);

public sealed record MyPermissionsResponse(IReadOnlyDictionary<string, ModulePermissions> Modules, IReadOnlyList<string> Roles);

public sealed class PermissionService : IPermissionService
{
    private readonly ICurrentUser _currentUser;
    private readonly IRolePermissionRepository _rolePermissions;

    public PermissionService(ICurrentUser currentUser, IRolePermissionRepository rolePermissions)
    {
        _currentUser = currentUser;
        _rolePermissions = rolePermissions;
    }

    public async Task<bool> HasAsync(string module, string action, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return false;
        if (_currentUser.HasAnyRole(TenebitRoles.Owner)) return true;

        var key = $"{module}.{action}";
        if (!RolePermissionKeys.DefaultAllowedRoles.TryGetValue(key, out var defaults))
        {
            // Unknown module/action pair - fail closed rather than silently allow.
            return false;
        }

        var overrides = await _rolePermissions.ListAsync(_currentUser.OrganizationId, cancellationToken);
        foreach (var role in _currentUser.Roles)
        {
            var overrideRow = overrides.FirstOrDefault(x => string.Equals(x.RoleKey, role, StringComparison.OrdinalIgnoreCase) && x.PermissionKey == key);
            var allowed = overrideRow?.Allowed ?? defaults.Contains(role, StringComparer.OrdinalIgnoreCase);
            if (allowed) return true;
        }

        return false;
    }

    public async Task<Result> EnsureAsync(string module, string action, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Error.Unauthorized());
        }

        if (await HasAsync(module, action, cancellationToken))
        {
            return Result.Success();
        }

        SecurityTelemetry.AuthorizationDenied();
        return Result.Failure(Error.Forbidden());
    }

    public async Task<IReadOnlyDictionary<string, ModulePermissions>> GetEffectivePermissionsAsync(CancellationToken cancellationToken)
    {
        var isOwner = _currentUser.HasAnyRole(TenebitRoles.Owner);
        var overrides = isOwner
            ? []
            : await _rolePermissions.ListAsync(_currentUser.OrganizationId, cancellationToken);

        var result = new Dictionary<string, ModulePermissions>();
        foreach (var module in PermissionModules.All)
        {
            var view = isOwner || HasEffective(overrides, module.Key, PermissionActions.View);
            var manage = module.SupportsManage && (isOwner || HasEffective(overrides, module.Key, PermissionActions.Manage));
            result[module.Key] = new ModulePermissions(view, manage);
        }

        return result;
    }

    private bool HasEffective(IReadOnlyList<Domain.Settings.RolePermission> overrides, string module, string action)
    {
        var key = $"{module}.{action}";
        if (!RolePermissionKeys.DefaultAllowedRoles.TryGetValue(key, out var defaults)) return false;

        foreach (var role in _currentUser.Roles)
        {
            var overrideRow = overrides.FirstOrDefault(x => string.Equals(x.RoleKey, role, StringComparison.OrdinalIgnoreCase) && x.PermissionKey == key);
            var allowed = overrideRow?.Allowed ?? defaults.Contains(role, StringComparer.OrdinalIgnoreCase);
            if (allowed) return true;
        }

        return false;
    }
}
