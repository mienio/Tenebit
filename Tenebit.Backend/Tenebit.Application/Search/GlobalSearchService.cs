using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Application.Licenses;
using Tenebit.Application.People;

namespace Tenebit.Application.Search;

public sealed record SearchHit(
    string Kind,
    Guid Id,
    string Title,
    string? Subtitle,
    string? Badge,
    /// <summary>Ready-to-use client route, already pointing at the record (see BuildUrl notes).</summary>
    string Url);

public sealed record GlobalSearchResponse(string Query, IReadOnlyList<SearchHit> Hits, bool Truncated);

/// <summary>
/// Cross-module quick search behind the Ctrl+K palette.
///
/// It deliberately delegates to the existing module services rather than querying repositories itself.
/// Each of those already enforces its own role gate and manager scope, so search inherits exactly the
/// visibility the user has elsewhere: a Manager sees only their team's assets here too, and someone with
/// no access to a module simply gets no hits from it instead of an error. Reimplementing the filters
/// here would be a second place for those rules to drift out of sync - and a likely way to leak records
/// a user cannot otherwise open.
/// </summary>
public sealed class GlobalSearchService
{
    /// <summary>Per-category cap. The palette shows a shortlist; the module pages do exhaustive listing.</summary>
    private const int PerKindLimit = 5;

    private readonly AssetService _assets;
    private readonly PeopleService _people;
    private readonly LocationService _locations;
    private readonly LicenseService _licenses;

    public GlobalSearchService(AssetService assets, PeopleService people, LocationService locations, LicenseService licenses)
    {
        _assets = assets;
        _people = people;
        _locations = locations;
        _licenses = licenses;
    }

    public async Task<GlobalSearchResponse> SearchAsync(string? query, CancellationToken cancellationToken)
    {
        var term = query?.Trim() ?? string.Empty;
        if (term.Length < 2)
        {
            // One character matches nearly everything; make the caller be specific rather than running
            // four module queries per keystroke.
            return new GlobalSearchResponse(term, [], false);
        }

        var hits = new List<SearchHit>();
        var truncated = false;

        Collect(await SearchAssetsAsync(term, cancellationToken));
        Collect(await SearchPeopleAsync(term, cancellationToken));
        Collect(await SearchLocationsAsync(term, cancellationToken));
        Collect(await SearchLicensesAsync(term, cancellationToken));

        void Collect((IReadOnlyList<SearchHit> Items, bool More) result)
        {
            hits.AddRange(result.Items);
            truncated |= result.More;
        }

        return new GlobalSearchResponse(term, hits, truncated);
    }

    private async Task<(IReadOnlyList<SearchHit> Items, bool More)> SearchAssetsAsync(string term, CancellationToken cancellationToken)
    {
        var result = await _assets.ListAsync(term, null, null, cancellationToken);
        if (result.IsFailure || result.Value is null) return ([], false);

        var matches = result.Value;
        return (matches.Take(PerKindLimit).Select(asset => new SearchHit(
            "asset",
            asset.Id,
            asset.Name,
            BuildSubtitle(asset.AssetTag, asset.SerialNumber, asset.AssignedPersonName ?? asset.Location),
            asset.CategoryName,
            // The assets page opens the detail panel directly for this query parameter.
            $"/assets?openAssetId={asset.Id}")).ToArray(), matches.Count > PerKindLimit);
    }

    private async Task<(IReadOnlyList<SearchHit> Items, bool More)> SearchPeopleAsync(string term, CancellationToken cancellationToken)
    {
        var result = await _people.ListAsync(term, cancellationToken);
        if (result.IsFailure || result.Value is null) return ([], false);

        var matches = result.Value;
        return (matches.Take(PerKindLimit).Select(person => new SearchHit(
            "person",
            person.Id,
            person.FullName,
            BuildSubtitle(person.Email, person.JobTitle, person.TeamName),
            person.IsActive ? null : "nieaktywny",
            // People has no per-record route, so the list is opened pre-filtered to this person.
            $"/people?search={Uri.EscapeDataString(person.FullName)}")).ToArray(), matches.Count > PerKindLimit);
    }

    private async Task<(IReadOnlyList<SearchHit> Items, bool More)> SearchLocationsAsync(string term, CancellationToken cancellationToken)
    {
        // Locations have no server-side search; the list is small (rooms/sites), so it is filtered here.
        var result = await _locations.ListAsync(cancellationToken);
        if (result.IsFailure || result.Value is null) return ([], false);

        var matches = result.Value
            .Where(location => Contains(location.Name, term) || Contains(location.FullPath, term))
            .ToArray();

        return (matches.Take(PerKindLimit).Select(location => new SearchHit(
            "location",
            location.Id,
            location.Name,
            location.FullPath,
            $"{location.AssetCount} aktywów",
            // Most useful landing for a location is the asset list scoped to it.
            $"/assets?location={Uri.EscapeDataString(location.Name)}")).ToArray(), matches.Length > PerKindLimit);
    }

    private async Task<(IReadOnlyList<SearchHit> Items, bool More)> SearchLicensesAsync(string term, CancellationToken cancellationToken)
    {
        var result = await _licenses.ListAsync(cancellationToken);
        if (result.IsFailure || result.Value is null) return ([], false);

        var matches = result.Value
            .Where(license => Contains(license.Name, term) || Contains(license.Vendor, term))
            .ToArray();

        return (matches.Take(PerKindLimit).Select(license => new SearchHit(
            "license",
            license.Id,
            license.Name,
            license.Vendor,
            null,
            "/licenses")).ToArray(), matches.Length > PerKindLimit);
    }

    private static bool Contains(string? value, string term) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string? BuildSubtitle(params string?[] parts)
    {
        var joined = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}
