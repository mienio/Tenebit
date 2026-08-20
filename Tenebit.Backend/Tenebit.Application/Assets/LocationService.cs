using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Assets;

public sealed class LocationService
{
    private static readonly string[] LocationManagers = [TenebitRoles.Owner, TenebitRoles.Admin];
    private static readonly string[] OrgWideInventoryRoles = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Auditor];

    private readonly ILocationRepository _locations;
    private readonly IAssetRepository _assets;
    private readonly IPersonRepository _people;
    private readonly IAssetCategoryRepository _categories;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ManagerScopeService _managerScope;
    private readonly ISubscriptionRepository _subscriptions;

    public LocationService(ILocationRepository locations, IAssetRepository assets, IPersonRepository people, IAssetCategoryRepository categories, ICurrentUser currentUser, IUnitOfWork unitOfWork, ManagerScopeService managerScope, ISubscriptionRepository subscriptions)
    {
        _locations = locations;
        _assets = assets;
        _people = people;
        _categories = categories;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _managerScope = managerScope;
        _subscriptions = subscriptions;
    }

    public async Task<Result<IReadOnlyList<LocationResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.LocationInventoryViewers);
        if (access.IsFailure) return Result<IReadOnlyList<LocationResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var rows = await _locations.ListAsync(organizationId, cancellationToken);
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideInventoryRoles, cancellationToken);
        var assets = scope is null
            ? await _assets.ListAsync(organizationId, null, null, null, cancellationToken)
            : await _assets.ListScopedAsync(organizationId, null, null, null, scope.PersonIds, scope.TeamIds, cancellationToken);
        IReadOnlyList<Person> people = [];
        if (_currentUser.HasAnyRole(TenebitRoles.PeopleViewers))
        {
            people = scope is null
                ? await _people.ListAsync(organizationId, null, cancellationToken)
                : await _people.ListScopedAsync(organizationId, null, scope.PersonIds, cancellationToken);
        }

        return Result<IReadOnlyList<LocationResponse>>.Success(MapLocations(rows, assets, people));
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
        var normalizedName = request.Name.Trim().ToUpperInvariant();
        if (existing.Any(x => x.ParentId == request.ParentId && x.NormalizedName == normalizedName))
        {
            return Result<LocationResponse>.Failure(Error.Conflict("Lokalizacja o tej nazwie już istnieje na tym poziomie."));
        }
        if (request.ParentId.HasValue && existing.All(x => x.Id != request.ParentId.Value))
        {
            return Result<LocationResponse>.Failure(Error.Validation("Lokalizacja nadrzędna nie istnieje."));
        }

        try
        {
            var location = new Location(organizationId, request.Name, request.Type, request.ParentId);

            var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
            if (subscription is null)
            {
                subscription = new OrganizationSubscription(organizationId, SubscriptionPlan.Free.Key);
                _subscriptions.Add(subscription);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            var limit = subscription.GetResourceLimit();

            var withinLimit = await _unitOfWork.ExecuteWithResourceLocksAsync(
                organizationId,
                "location-capacity",
                [organizationId],
                async ct =>
            {
                var currentCount = await _locations.CountAsync(organizationId, ct);
                if (currentCount >= limit) return false;

                _locations.Add(location);
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            if (!withinLimit)
            {
                var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
                return Result<LocationResponse>.Failure(Error.Validation($"Limit lokalizacji przekroczony. Plan {plan.Name} pozwala na {limit} lokalizacji. Przejdź na wyższy plan."));
            }

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

        var normalizedName = request.Name.Trim().ToUpperInvariant();
        if (existing.Any(x => x.Id != id && x.ParentId == request.ParentId && x.NormalizedName == normalizedName))
        {
            return Result<LocationResponse>.Failure(Error.Conflict("Lokalizacja o tej nazwie już istnieje na tym poziomie."));
        }

        if (request.ParentId.HasValue)
        {
            if (!byId.ContainsKey(request.ParentId.Value))
            {
                return Result<LocationResponse>.Failure(Error.Validation("Lokalizacja nadrzędna nie istnieje."));
            }

            if (Location.WouldCreateCycle(id, request.ParentId.Value, byId))
            {
                return Result<LocationResponse>.Failure(Error.Validation("Nie można ustawić lokalizacji podrzędnej jako nadrzędnej - utworzyłoby to cykl."));
            }
        }

        try
        {
            location.Update(request.Name, request.Type, request.ParentId, request.IsActive);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var all = await _locations.ListAsync(organizationId, cancellationToken);
            var allById = all.ToDictionary(x => x.Id);
            var paths = all.ToDictionary(x => x.Id, x => Location.BuildFullPath(x, allById));
            var assetsToRefresh = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
            foreach (var asset in assetsToRefresh.Where(x => x.LocationId.HasValue && paths.ContainsKey(x.LocationId.Value)))
                asset.SetLocation(asset.LocationId, paths[asset.LocationId!.Value]);
            var peopleToRefresh = await _people.ListAsync(organizationId, null, cancellationToken);
            foreach (var person in peopleToRefresh.Where(x => x.LocationId.HasValue && paths.ContainsKey(x.LocationId.Value)))
                person.SetLocation(person.LocationId, paths[person.LocationId!.Value]);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        var assetCount = await _assets.CountByLocationIdAsync(organizationId, id, cancellationToken);
        var personCount = await _people.CountByLocationIdAsync(organizationId, id, cancellationToken);
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
        var target = rows.FirstOrDefault(x => x.Id == id);
        if (target is null)
        {
            return Result<LocationInventoryResponse>.Failure(Error.NotFound("Lokalizacja nie istnieje."));
        }

        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideInventoryRoles, cancellationToken);
        var assets = scope is null
            ? await _assets.ListAsync(organizationId, null, null, null, cancellationToken)
            : await _assets.ListScopedAsync(organizationId, null, null, null, scope.PersonIds, scope.TeamIds, cancellationToken);
        IReadOnlyList<Person> people = [];
        if (_currentUser.HasAnyRole(TenebitRoles.PeopleViewers))
        {
            people = scope is null
                ? await _people.ListAsync(organizationId, null, cancellationToken)
                : await _people.ListScopedAsync(organizationId, null, scope.PersonIds, cancellationToken);
        }
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var mapped = MapLocations(rows, assets, people);
        var location = mapped.First(x => x.Id == id);

        var locationAssets = assets
            .Where(x => x.LocationId == id)
            .Select(asset => MapAsset(asset, categories, people))
            .ToList();
        var locationPeople = people
            .Where(x => x.LocationId == id)
            .Select(MapPerson)
            .ToList();

        return Result<LocationInventoryResponse>.Success(new LocationInventoryResponse(location, locationAssets, locationPeople));
    }

    private static IReadOnlyList<LocationResponse> MapLocations(IReadOnlyList<Location> rows, IReadOnlyList<Asset> assets, IReadOnlyList<Person> people)
    {
        var byId = rows.ToDictionary(x => x.Id);
        return rows.Select(row =>
        {
            var path = Location.BuildFullPath(row, byId);
            var descendantIds = rows.Where(candidate => IsDescendantOrSelf(candidate, row.Id, byId)).Select(x => x.Id).ToHashSet();
            var assetCount = assets.Count(x => x.LocationId.HasValue && descendantIds.Contains(x.LocationId.Value));
            var personCount = people.Count(x => x.LocationId.HasValue && descendantIds.Contains(x.LocationId.Value));
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

    private static bool IsDescendantOrSelf(Location candidate, Guid ancestorId, IReadOnlyDictionary<Guid, Location> byId)
    {
        Guid? current = candidate.Id;
        var visited = new HashSet<Guid>();
        while (current.HasValue && visited.Add(current.Value))
        {
            if (current.Value == ancestorId) return true;
            current = byId.TryGetValue(current.Value, out var node) ? node.ParentId : null;
        }
        return false;
    }

    private static AssetListItem MapAsset(Asset asset, IReadOnlyList<AssetCategory> categories, IReadOnlyList<Person> people)
    {
        var category = categories.FirstOrDefault(c => c.Id == asset.CategoryId);
        var assignedPerson = asset.AssignedPersonId.HasValue ? people.FirstOrDefault(p => p.Id == asset.AssignedPersonId) : null;
        return new(asset.Id, asset.Name, asset.AssetTag, asset.CategoryId, category?.Name, asset.Status, asset.AssignedPersonId, assignedPerson?.FullName, asset.Location, asset.PurchasePrice, asset.Currency, asset.WarrantyUntil);
    }
    private static PersonListItem MapPerson(Person person) => new(person.Id, person.FullName, person.Email, person.JobTitle, person.ManagerId, person.Location);
}

[ValidatedRequest]
public sealed record CreateLocationRequest(string Name, string? Type, Guid? ParentId);
[ValidatedRequest]
public sealed record UpdateLocationRequest(string Name, string? Type, Guid? ParentId, bool IsActive);
public sealed record LocationResponse(Guid Id, string Name, string Type, Guid? ParentId, string FullPath, int AssetCount, int PersonCount, bool IsActive);
public sealed record AssetListItem(Guid Id, string Name, string AssetTag, Guid CategoryId, string? CategoryName, AssetStatus Status, Guid? AssignedPersonId, string? AssignedPersonName, string? Location, decimal? PurchasePrice, string? Currency, DateOnly? WarrantyUntil);
public sealed record PersonListItem(Guid Id, string FullName, string Email, string? JobTitle, Guid? ManagerId, string? Location);
public sealed record LocationInventoryResponse(LocationResponse Location, IReadOnlyList<AssetListItem> Assets, IReadOnlyList<PersonListItem> People);
