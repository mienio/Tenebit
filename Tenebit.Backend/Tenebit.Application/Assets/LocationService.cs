using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;

namespace Tenebit.Application.Assets;

public sealed class LocationService
{
    // Mutacje struktury lokalizacji zmieniają dane całej organizacji — ta sama granica ról co
    // pozostałe zakładki "organizationOnly" w ustawieniach (patrz SettingsPage.tsx canManageOrganization).
    private static readonly string[] LocationManagers = [TenebitRoles.Owner, TenebitRoles.Admin];

    private static readonly string[] OrgWideInventoryRoles = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Auditor];

    private readonly ILocationRepository _locations;
    private readonly IAssetRepository _assets;
    private readonly IPersonRepository _people;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ManagerScopeService _managerScope;

    public LocationService(ILocationRepository locations, IAssetRepository assets, IPersonRepository people, ICurrentUser currentUser, IUnitOfWork unitOfWork, ManagerScopeService managerScope)
    {
        _locations = locations;
        _assets = assets;
        _people = people;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _managerScope = managerScope;
    }

    public async Task<IReadOnlyList<LocationResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var locations = await _locations.ListAsync(organizationId, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        return MapLocations(locations, assets, people);
    }

    public async Task<Result<LocationResponse>> CreateAsync(CreateLocationRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, LocationManagers);
        if (access.IsFailure) return Result<LocationResponse>.Failure(access.Error!);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<LocationResponse>.Failure(Error.Validation("Nazwa lokalizacji jest wymagana."));
        }

        var organizationId = _currentUser.OrganizationId;
        var existing = await _locations.ListAsync(organizationId, cancellationToken);
        if (request.ParentId.HasValue && existing.All(x => x.Id != request.ParentId.Value))
        {
            return Result<LocationResponse>.Failure(Error.Validation("Lokalizacja nadrzędna nie istnieje."));
        }

        try
        {
            var location = new Location(organizationId, request.Name, request.Type, request.ParentId);
            _locations.Add(location);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var all = await _locations.ListAsync(organizationId, cancellationToken);
            var response = MapLocations(all, [], []).First(x => x.Id == location.Id);
            return Result<LocationResponse>.Success(response);
        }
        catch (DomainException ex)
        {
            return Result<LocationResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<LocationResponse>> UpdateAsync(Guid id, UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, LocationManagers);
        if (access.IsFailure) return Result<LocationResponse>.Failure(access.Error!);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<LocationResponse>.Failure(Error.Validation("Nazwa lokalizacji jest wymagana."));
        }

        if (request.ParentId == id)
        {
            return Result<LocationResponse>.Failure(Error.Validation("Lokalizacja nie może być nadrzędna sama dla siebie."));
        }

        var organizationId = _currentUser.OrganizationId;
        var existing = await _locations.ListAsync(organizationId, cancellationToken);
        var byId = existing.ToDictionary(x => x.Id);
        if (!byId.TryGetValue(id, out var location))
        {
            return Result<LocationResponse>.Failure(Error.NotFound("Lokalizacja nie istnieje."));
        }

        if (request.ParentId.HasValue)
        {
            if (!byId.ContainsKey(request.ParentId.Value))
            {
                return Result<LocationResponse>.Failure(Error.Validation("Lokalizacja nadrzędna nie istnieje."));
            }

            if (Location.WouldCreateCycle(id, request.ParentId.Value, byId))
            {
                return Result<LocationResponse>.Failure(Error.Validation("Nie można ustawić lokalizacji podrzędnej jako nadrzędnej — utworzyłoby to cykl."));
            }
        }

        try
        {
            location.Update(request.Name, request.Type, request.ParentId, request.IsActive);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var all = await _locations.ListAsync(organizationId, cancellationToken);
            var response = MapLocations(all, [], []).First(x => x.Id == id);
            return Result<LocationResponse>.Success(response);
        }
        catch (DomainException ex)
        {
            return Result<LocationResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, LocationManagers);
        if (access.IsFailure) return access;

        var organizationId = _currentUser.OrganizationId;
        var rows = await _locations.ListAsync(organizationId, cancellationToken);
        var target = rows.FirstOrDefault(x => x.Id == id);
        if (target is null)
        {
            return Result.Failure(Error.NotFound("Lokalizacja nie istnieje."));
        }

        if (rows.Any(x => x.ParentId == id))
        {
            return Result.Failure(Error.Validation("Najpierw usuń podlokalizacje tej pozycji."));
        }

        var byId = rows.ToDictionary(x => x.Id);
        var fullPath = Location.BuildFullPath(target, byId);

        var assetCount = await _assets.CountByLocationAsync(organizationId, fullPath, cancellationToken);
        var personCount = await _people.CountByLocationAsync(organizationId, fullPath, cancellationToken);
        if (assetCount > 0 || personCount > 0)
        {
            return Result.Failure(Error.Validation("Nie można usunąć lokalizacji z przypisanymi aktywami albo osobami."));
        }

        _locations.Remove(target);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<LocationInventoryResponse>> GetInventoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.LocationInventoryViewers);
        if (access.IsFailure) return Result<LocationInventoryResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var rows = await _locations.ListAsync(organizationId, cancellationToken);
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var mapped = MapLocations(rows, assets, people);
        var location = mapped.FirstOrDefault(x => x.Id == id);
        if (location is null)
        {
            return Result<LocationInventoryResponse>.Failure(Error.NotFound("Lokalizacja nie istnieje."));
        }

        var visibleIds = await _managerScope.ResolveVisiblePersonIdsAsync(_currentUser, OrgWideInventoryRoles, cancellationToken);

        var locationAssets = assets
            .Where(x => string.Equals(x.Location, location.FullPath, StringComparison.OrdinalIgnoreCase))
            .Where(x => visibleIds is null || x.AssignedPersonId is null || visibleIds.Contains(x.AssignedPersonId.Value))
            .Select(MapAsset).ToList();
        var locationPeople = people
            .Where(x => string.Equals(x.Location, location.FullPath, StringComparison.OrdinalIgnoreCase))
            .Where(x => visibleIds is null || visibleIds.Contains(x.Id))
            .Select(MapPerson).ToList();
        return Result<LocationInventoryResponse>.Success(new LocationInventoryResponse(location, locationAssets, locationPeople));
    }

    private static IReadOnlyList<LocationResponse> MapLocations(IReadOnlyList<Location> rows, IReadOnlyList<Asset> assets, IReadOnlyList<Person> people)
    {
        var byId = rows.ToDictionary(x => x.Id);
        return rows.Select(row =>
        {
            var path = Location.BuildFullPath(row, byId);
            var pathPrefix = path + " / ";
            var assetCount = assets.Count(x =>
                string.Equals(x.Location, path, StringComparison.OrdinalIgnoreCase)
                || (x.Location != null && x.Location.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase)));
            var personCount = people.Count(x =>
                string.Equals(x.Location, path, StringComparison.OrdinalIgnoreCase)
                || (x.Location != null && x.Location.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase)));
            return new LocationResponse(
                row.Id,
                row.Name,
                row.Type,
                row.ParentId,
                path,
                assetCount,
                personCount,
                row.IsActive);
        }).OrderBy(x => x.FullPath).ToList();
    }

    private static AssetListItem MapAsset(Asset asset) => new(asset.Id, asset.Name, asset.AssetTag, asset.Status, asset.AssignedPersonId, asset.Location);
    private static PersonListItem MapPerson(Person person) => new(person.Id, person.FullName, person.Email, person.JobTitle, person.ManagerId, person.Location);
}

public sealed record CreateLocationRequest(string Name, string? Type, Guid? ParentId);
public sealed record UpdateLocationRequest(string Name, string? Type, Guid? ParentId, bool IsActive);
public sealed record LocationResponse(Guid Id, string Name, string Type, Guid? ParentId, string FullPath, int AssetCount, int PersonCount, bool IsActive);
public sealed record AssetListItem(Guid Id, string Name, string AssetTag, AssetStatus Status, Guid? AssignedPersonId, string? Location);
public sealed record PersonListItem(Guid Id, string FullName, string Email, string? JobTitle, Guid? ManagerId, string? Location);
public sealed record LocationInventoryResponse(LocationResponse Location, IReadOnlyList<AssetListItem> Assets, IReadOnlyList<PersonListItem> People);
