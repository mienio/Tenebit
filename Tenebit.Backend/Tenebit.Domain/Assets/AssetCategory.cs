using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assets;

public sealed class AssetCategory
{
    private AssetCategory() { }

    public AssetCategory(Guid organizationId, string name, AssetCategoryType type, string? description, string? icon = null, bool isSystem = false, int sortOrder = 1000, ReturnHandlingMode returnHandlingMode = ReturnHandlingMode.DirectToStock, PostReturnDisposition postReturnDisposition = PostReturnDisposition.Reuse, string? returnChecklistTemplate = null, PhotoRequirement photoOnIssue = PhotoRequirement.Disabled, PhotoRequirement photoOnReturn = PhotoRequirement.Disabled)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        IsSystem = isSystem;
        SortOrder = sortOrder;
        CreatedAt = DateTimeOffset.UtcNow;
        Update(name, type, description, icon);
        UpdateReturnPolicy(returnHandlingMode, postReturnDisposition, returnChecklistTemplate, photoOnIssue, photoOnReturn);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AssetCategoryType Type { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public bool IsSystem { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<AssetFieldDefinition> FieldDefinitions { get; private set; } = [];
    public ReturnHandlingMode ReturnHandlingMode { get; private set; } = ReturnHandlingMode.DirectToStock;
    public PostReturnDisposition PostReturnDisposition { get; private set; } = PostReturnDisposition.Reuse;
    public string? ReturnChecklistTemplate { get; private set; }
    public PhotoRequirement PhotoOnIssue { get; private set; } = PhotoRequirement.Disabled;
    public PhotoRequirement PhotoOnReturn { get; private set; } = PhotoRequirement.Disabled;

    public void Update(string name, AssetCategoryType type, string? description, string? icon)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa kategorii jest wymagana.");
        }

        Name = name.Trim();
        Type = type;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
    }

    public void UpdateReturnPolicy(ReturnHandlingMode returnHandlingMode, PostReturnDisposition postReturnDisposition, string? returnChecklistTemplate, PhotoRequirement photoOnIssue, PhotoRequirement photoOnReturn)
    {
        ReturnHandlingMode = returnHandlingMode;
        PostReturnDisposition = postReturnDisposition;
        ReturnChecklistTemplate = string.IsNullOrWhiteSpace(returnChecklistTemplate) ? null : returnChecklistTemplate.Trim();
        PhotoOnIssue = photoOnIssue;
        PhotoOnReturn = photoOnReturn;
    }

    // Diff po Key zamiast Clear()+Add(): niezmienione pola zostają jako te same wiersze (Id bez zmian),
    // więc zapis dotyka w bazie tylko realnie usuniętych/dodanych/zmienionych pozycji, a nie całej kolekcji
    // przy każdej edycji (patrz audyt AUD-021 — DbUpdateConcurrencyException przy pełnym replace).
    public void ReplaceFieldDefinitions(IEnumerable<(string Key, string Label, AssetFieldType FieldType, string? Options, bool Required)> definitions)
    {
        var incoming = definitions.ToList();
        var incomingKeys = incoming.Select(x => x.Key).ToHashSet();

        FieldDefinitions.RemoveAll(existing => !incomingKeys.Contains(existing.Key));

        var sortOrder = 0;
        foreach (var definition in incoming)
        {
            var existing = FieldDefinitions.FirstOrDefault(x => x.Key == definition.Key);
            if (existing is not null)
            {
                existing.UpdateDetails(definition.Label, definition.FieldType, definition.Options, definition.Required, sortOrder);
            }
            else
            {
                FieldDefinitions.Add(new AssetFieldDefinition(Id, definition.Key, definition.Label, definition.FieldType, definition.Options, definition.Required, sortOrder));
            }

            sortOrder++;
        }
    }
}
