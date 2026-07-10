using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assets;

public sealed class Asset
{
    private Asset() { }

    public Asset(Guid organizationId, Guid categoryId, string name, string assetTag)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CategoryId = categoryId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        Status = AssetStatus.InStock;
        UpdateCore(name, assetTag, null, null, null, null, null, null, null, null, null, null);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string AssetTag { get; private set; } = string.Empty;
    public string? SerialNumber { get; private set; }
    public AssetStatus Status { get; private set; }
    public Guid? AssignedPersonId { get; private set; }
    public Guid? TeamId { get; private set; }
    public string? Location { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public decimal? PurchasePrice { get; private set; }
    public string? Currency { get; private set; }
    public DateOnly? PurchaseDate { get; private set; }
    public DateOnly? WarrantyUntil { get; private set; }
    public string QrCodePayload { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<AssetFieldValue> FieldValues { get; private set; } = [];

    public void SetFieldValues(IReadOnlyDictionary<string, string> values)
    {
        FieldValues.Clear();
        foreach (var (key, value) in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            FieldValues.Add(new AssetFieldValue(Id, key, value.Trim()));
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateCore(
        string name,
        string assetTag,
        string? serialNumber,
        Guid? categoryId,
        string? location,
        string? manufacturer,
        string? model,
        decimal? purchasePrice,
        string? currency,
        DateOnly? purchaseDate,
        DateOnly? warrantyUntil,
        Guid? teamId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa aktywa jest wymagana.");
        }

        if (string.IsNullOrWhiteSpace(assetTag))
        {
            throw new DomainException("Tag aktywa jest wymagany.");
        }

        if (purchasePrice is < 0)
        {
            throw new DomainException("Cena zakupu nie może być ujemna.");
        }

        Name = name.Trim();
        AssetTag = assetTag.Trim();
        QrCodePayload = AssetTag;
        SerialNumber = Normalize(serialNumber);
        if (categoryId.HasValue)
        {
            CategoryId = categoryId.Value;
        }

        Location = Normalize(location);
        Manufacturer = Normalize(manufacturer);
        Model = Normalize(model);
        PurchasePrice = purchasePrice;
        Currency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
        PurchaseDate = purchaseDate;
        WarrantyUntil = warrantyUntil;
        TeamId = teamId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeStatus(AssetStatus status)
    {
        if (Status == AssetStatus.Disposed && status != AssetStatus.Disposed)
        {
            throw new DomainException("Zutylizowane aktywo nie może wrócić do obiegu.");
        }

        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssignTo(Guid personId)
    {
        if (Status is AssetStatus.Assigned or AssetStatus.Disposed or AssetStatus.Lost)
        {
            throw new DomainException("Aktywo nie jest dostępne do wydania.");
        }

        AssignedPersonId = personId;
        Status = AssetStatus.Assigned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ReturnToStock(string? location)
    {
        AssignedPersonId = null;
        Location = Normalize(location) ?? Location;
        Status = AssetStatus.InStock;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
