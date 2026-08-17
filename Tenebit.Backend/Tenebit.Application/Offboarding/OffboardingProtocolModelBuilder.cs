using Tenebit.Application.Abstractions;
using Tenebit.Domain.Offboarding;

namespace Tenebit.Application.Offboarding;

/// <summary>Buduje dane wejściowe do generatora PDF protokołu offboardingu — wydzielone z OffboardingService
/// (audyt P2 #3). Sam generator (IPdfProtocolGenerator) zostaje wywoływany przez OffboardingService, ta klasa
/// odpowiada wyłącznie za zebranie i zmapowanie danych.</summary>
public sealed class OffboardingProtocolModelBuilder
{
    private readonly IOrganizationRepository _organizations;
    private readonly IPersonRepository _people;
    private readonly IOffboardingItemRepository _items;
    private readonly IAssetRepository _assets;
    private readonly IAssetEvidenceRepository _evidence;
    private readonly ILicenseRepository _licenses;

    public OffboardingProtocolModelBuilder(IOrganizationRepository organizations, IPersonRepository people, IOffboardingItemRepository items,
        IAssetRepository assets, IAssetEvidenceRepository evidence, ILicenseRepository licenses)
    {
        _organizations = organizations;
        _people = people;
        _items = items;
        _assets = assets;
        _evidence = evidence;
        _licenses = licenses;
    }

    public async Task<OffboardingProtocolPdfModel> BuildAsync(OffboardingCase offboardingCase, CancellationToken cancellationToken)
    {
        var organizationId = offboardingCase.OrganizationId;
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var person = await _people.GetAsync(organizationId, offboardingCase.PersonId, cancellationToken);
        var items = await _items.ListByCaseAsync(organizationId, offboardingCase.Id, cancellationToken);

        var assetItems = items.Where(x => x.Type == OffboardingItemType.AssetReturn).ToList();
        var licenseItems = items.Where(x => x.Type == OffboardingItemType.LicenseRelease).ToList();

        var assetRows = new List<OffboardingProtocolAssetRow>();
        var photos = new List<OffboardingProtocolPhoto>();
        foreach (var item in assetItems)
        {
            var asset = item.AssetId.HasValue ? await _assets.GetAsync(organizationId, item.AssetId.Value, cancellationToken) : null;
            assetRows.Add(new OffboardingProtocolAssetRow(asset?.Name ?? item.Label, asset?.AssetTag ?? "—", item.Status.ToString(),
                item.ResolutionNotes, item.CompletedBy, item.CompletedAt));

            if (item.AssetId.HasValue)
            {
                var evidenceForAsset = await _evidence.ListByAssetAsync(organizationId, item.AssetId.Value, cancellationToken);
                photos.AddRange(evidenceForAsset
                    .Where(e => e.OffboardingItemId == item.Id && e.Content.Length > 0)
                    .Select(e => new OffboardingProtocolPhoto(e.FileName, e.ContentType, e.Content, e.Sha256)));
            }
        }

        var licenseRows = new List<OffboardingProtocolLicenseRow>();
        foreach (var item in licenseItems.Where(x => x.Status == OffboardingItemStatus.Released))
        {
            var license = item.LicenseId.HasValue ? await _licenses.GetAsync(organizationId, item.LicenseId.Value, cancellationToken) : null;
            licenseRows.Add(new OffboardingProtocolLicenseRow(license?.Name ?? item.Label, item.CompletedAt, item.CompletedBy));
        }

        var exceptionStatuses = new[] { OffboardingItemStatus.Missing, OffboardingItemStatus.Damaged, OffboardingItemStatus.Retained, OffboardingItemStatus.Waived };
        var exceptions = items
            .Where(x => exceptionStatuses.Contains(x.Status))
            .Select(x => new OffboardingProtocolExceptionRow(x.Label, x.Status.ToString(), x.ResolutionNotes, ResolveActorKind(x), x.CompletedAt))
            .ToList();

        var requiredItems = items.Where(x => x.Required).ToList();
        var hasExceptions = requiredItems.Any(x => exceptionStatuses.Contains(x.Status));
        var outcome = hasExceptions ? "Rozliczony z wyjątkami" : "Rozliczony";

        return new OffboardingProtocolPdfModel(
            organization?.Name ?? "Tenebit",
            organization?.LogoUrl,
            organization?.Country ?? "PL",
            offboardingCase.FinalProtocolNumber ?? string.Empty,
            person?.FullName ?? "—",
            offboardingCase.StartedAt,
            offboardingCase.ReturnDueDate,
            offboardingCase.CompletedAt ?? offboardingCase.ReturnDueDate,
            assetRows,
            licenseRows,
            exceptions,
            photos,
            outcome,
            offboardingCase.Notes);
    }

    /// <summary>Kto rozstrzygnął pozycję — jeśli sam pracownik zgłosił odpowiedź (EmployeeResponse) i nikt inny
    /// jej nie nadpisał, uznajemy rozstrzygnięcie za pochodzące od pracownika; "system" jako CompletedBy oznacza
    /// automatyzację; w pozostałych przypadkach rozstrzygnął administrator/operator.</summary>
    private static string ResolveActorKind(OffboardingItem item)
    {
        if (item.CompletedBy == "system") return "automatyzacja";
        if (!string.IsNullOrWhiteSpace(item.EmployeeResponse) && string.IsNullOrWhiteSpace(item.ResolutionNotes)) return "pracownik";
        return item.CompletedBy ?? "administrator";
    }

    public static string CreateProtocolNumber(DateTimeOffset now) => $"OFF-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
