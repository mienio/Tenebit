using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Assets;

/// <summary>
/// Resolves the legacy/display location path to the stable tenant-owned Location.Id. Services keep the
/// path only as a denormalized display/cache value; authorization, inventory and referential integrity
/// use the ID.
/// </summary>
public sealed class LocationReferenceService
{
    private readonly ILocationRepository _locations;

    public LocationReferenceService(ILocationRepository locations) => _locations = locations;

    public async Task<(Guid? Id, string? FullPath)> ResolveAsync(Guid organizationId, string? requestedPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedPath)) return (null, null);

        var locations = await _locations.ListAsync(organizationId, cancellationToken);
        var byId = locations.ToDictionary(x => x.Id);
        var requested = requestedPath.Trim();
        foreach (var location in locations)
        {
            var fullPath = Location.BuildFullPath(location, byId);
            if (string.Equals(fullPath, requested, StringComparison.OrdinalIgnoreCase))
            {
                return (location.Id, fullPath);
            }
        }

        throw new DomainException("Wybrana lokalizacja nie istnieje w tej organizacji.");
    }
}
