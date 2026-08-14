using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;

namespace Tenebit.Application.Reservations;

/// <summary>GET /api/reservation-catalog (spec 8.9) — podgląd katalogu rezerwowalnych kategorii i zestawów
/// wraz z orientacyjną liczbą dostępnych sztuk. Nie tworzy wniosku (to kolejne zadanie).</summary>
public sealed class ReservationCatalogService
{
    private readonly IPersonRepository _people;
    private readonly IAssetCategoryRepository _categories;
    private readonly IEquipmentKitDefinitionRepository _kits;
    private readonly AssetAvailabilityService _availability;
    private readonly ICurrentUser _currentUser;

    public ReservationCatalogService(IPersonRepository people, IAssetCategoryRepository categories, IEquipmentKitDefinitionRepository kits, AssetAvailabilityService availability, ICurrentUser currentUser)
    {
        _people = people;
        _categories = categories;
        _kits = kits;
        _availability = availability;
        _currentUser = currentUser;
    }

    public async Task<ReservationCatalogResponse> GetAsync(DateTimeOffset from, DateTimeOffset to, string? search, string? location, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;

        // Portal jest dostępny wyłącznie dla konta powiązanego z Person (spec 8.6), tak samo jak MyWorkspace.
        var person = string.IsNullOrEmpty(_currentUser.Email) ? null : await _people.FindByEmailAsync(organizationId, _currentUser.Email, cancellationToken);
        if (person is null)
        {
            return new ReservationCatalogResponse(false, [], []);
        }

        var allCategories = await _categories.ListAsync(organizationId, cancellationToken);

        var categories = new List<ReservationCatalogCategoryResponse>();
        foreach (var category in allCategories.Where(c => c.VisibleInEmployeeCatalog && MatchesSearch(c.CatalogName ?? c.Name, c.CatalogDescription, search)))
        {
            var available = await _availability.CountAvailableAsync(organizationId, category.Id, from, to, location, cancellationToken);
            categories.Add(new ReservationCatalogCategoryResponse(category.Id, category.CatalogName ?? category.Name, category.CatalogDescription, category.CatalogImageUrl, category.ReservationMode, available));
        }

        var kitDefinitions = await _kits.ListAsync(organizationId, cancellationToken);

        var kits = new List<ReservationCatalogKitResponse>();
        foreach (var kit in kitDefinitions.Where(k => k.VisibleInEmployeeCatalog && MatchesSearch(k.Name, k.Description, search)))
        {
            var items = new List<ReservationCatalogKitItemResponse>();
            var completeKitsAvailable = 0;
            var first = true;
            foreach (var item in kit.Items)
            {
                var available = await _availability.CountAvailableAsync(organizationId, item.AssetCategoryId, from, to, location, cancellationToken);
                var category = allCategories.FirstOrDefault(c => c.Id == item.AssetCategoryId);
                items.Add(new ReservationCatalogKitItemResponse(item.AssetCategoryId, category?.CatalogName ?? category?.Name ?? "Kategoria", item.RequiredQuantity));

                var possibleKits = available / item.RequiredQuantity;
                completeKitsAvailable = first ? possibleKits : Math.Min(completeKitsAvailable, possibleKits);
                first = false;
            }

            kits.Add(new ReservationCatalogKitResponse(kit.Id, kit.Name, kit.Description, completeKitsAvailable, items));
        }

        return new ReservationCatalogResponse(true, categories, kits);
    }

    private static bool MatchesSearch(string name, string? description, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var term = search.Trim();
        return name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (description is not null && description.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
