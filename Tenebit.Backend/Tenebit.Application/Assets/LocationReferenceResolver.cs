using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;

namespace Tenebit.Application.Assets;

/// <summary>Resolves the user-facing cached location path to the stable tenant-local LocationId.</summary>
public sealed class LocationReferenceResolver
{
    private readonly ILocationRepository _locations;

    public LocationReferenceResolver(ILocationRepository locations) => _locations = locations;

    public async Task<Result<LocationReference>> ResolveAsync(Guid organizationId, string? requestedPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return Result<LocationReference>.Success(new LocationReference(null, null));
        }

        var rows = await _locations.ListAsync(organizationId, cancellationToken);
        var byId = rows.ToDictionary(x => x.Id);
        var trimmed = requestedPath.Trim();
        var matches = rows
            .Select(x => new LocationReference(x.Id, Location.BuildFullPath(x, byId)))
            .Where(x => string.Equals(x.FullPath, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => Result<LocationReference>.Success(matches[0]),
            0 => Result<LocationReference>.Failure(Error.Validation("Wybrana lokalizacja nie istnieje.")),
            _ => Result<LocationReference>.Failure(Error.Conflict("Ścieżka lokalizacji jest niejednoznaczna."))
        };
    }
}

public sealed record LocationReference(Guid? Id, string? FullPath);
