using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Assets;

/// <summary>
/// One resource guard for every read path that exposes an asset or an asset-owned subresource.
/// Plain Managers are restricted to their managed people/teams; a resource outside that scope is
/// deliberately returned as NotFound to avoid existence disclosure.
/// </summary>
public sealed class AssetAuthorizationService
{
    private static readonly string[] OrganizationWideAssetReaders =
    [
        TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician,
        TenebitRoles.Hr, TenebitRoles.LicenseManager, TenebitRoles.Finance, TenebitRoles.Auditor
    ];

    private readonly IAssetRepository _assets;
    private readonly ManagerScopeService _managerScope;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;

    public AssetAuthorizationService(IAssetRepository assets, ManagerScopeService managerScope, ICurrentUser currentUser, IPermissionService permissions)
    {
        _assets = assets;
        _managerScope = managerScope;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<Result<Asset>> EnsureCanViewAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var access = await _permissions.EnsureAsync(PermissionModules.Assets, PermissionActions.View, cancellationToken);
        if (access.IsFailure) return Result<Asset>.Failure(access.Error!);

        var asset = await _assets.GetAsync(_currentUser.OrganizationId, assetId, cancellationToken);
        if (asset is null) return Result<Asset>.Failure(Error.NotFound("Aktywo nie istnieje."));

        var scope = await _managerScope.ResolveAsync(_currentUser, OrganizationWideAssetReaders, cancellationToken);
        if (scope is not null && !scope.ContainsAsset(asset.AssignedPersonId, asset.TeamId))
        {
            return Result<Asset>.Failure(Error.NotFound("Aktywo nie istnieje."));
        }

        return Result<Asset>.Success(asset);
    }

    public Task<ManagerAccessScope?> ResolveListScopeAsync(CancellationToken cancellationToken) =>
        _managerScope.ResolveAsync(_currentUser, OrganizationWideAssetReaders, cancellationToken);
}
