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
    public bool IsReservable { get; private set; }
    public string? ReservationInstructions { get; private set; }
    public int? MaxReservationDays { get; private set; }

    public void SetReservationSettings(bool isReservable, string? instructions, int? maxDays)
    {
        if (maxDays is <= 0)
        {
            throw new DomainException("Maksymalna liczba dni rezerwacji musi być większa od zera.");
        }

        IsReservable = isReservable;
        ReservationInstructions = Normalize(instructions);
        MaxReservationDays = maxDays;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

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
        if (Status is AssetStatus.Assigned or AssetStatus.Disposed or AssetStatus.Lost or AssetStatus.PendingReturn)
        {
            throw new DomainException("Aktywo nie jest dostępne do wydania.");
        }

        AssignedPersonId = personId;
        Status = AssetStatus.Assigned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkPendingReturn()
    {
        if (Status == AssetStatus.PendingReturn) return;
        if (Status != AssetStatus.Assigned)
        {
            throw new DomainException("Tylko wydane aktywo można oznaczyć jako oczekujące na zwrot.");
        }

        Status = AssetStatus.PendingReturn;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Cofnięcie <see cref="MarkPendingReturn"/> — używane przy anulowaniu offboardingu dla pozycji,
    /// których nie zdążono jeszcze fizycznie zwrócić (spec 4.4).</summary>
    public void RestorePendingReturn(Guid personId)
    {
        if (Status != AssetStatus.PendingReturn) return;

        AssignedPersonId = personId;
        Status = AssetStatus.Assigned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ReturnToStock(string? location) => ReleaseAssignment(AssetStatus.InStock, location);

    /// <summary>Korekta błędu ewidencji (spec 5.7 rozstrzygnięcie OwnershipCorrected) — w przeciwieństwie do
    /// <see cref="AssignTo"/> celowo NIE sprawdza obecnego statusu (aktywo może być już Assigned do kogoś innego,
    /// to właśnie ten błąd naprawiamy), poza odrzuceniem zutylizowanego aktywa.</summary>
    public void CorrectOwner(Guid newPersonId)
    {
        if (Status == AssetStatus.Disposed)
        {
            throw new DomainException("Zutylizowanego aktywa nie można przypisać.");
        }

        AssignedPersonId = newPersonId;
        Status = AssetStatus.Assigned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ReleaseAssignment(AssetStatus status, string? location = null)
    {
        if (Status == AssetStatus.Disposed && status != AssetStatus.Disposed)
        {
            throw new DomainException("Zutylizowane aktywo nie może wrócić do obiegu.");
        }

        AssignedPersonId = null;
        if (location is not null) Location = Normalize(location) ?? Location;
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
