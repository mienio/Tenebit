using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assets;

public sealed class AssetCategory
{
    private AssetCategory() { }

    public AssetCategory(Guid organizationId, string name, AssetCategoryType type, string? description, string? icon = null, bool isSystem = false, int sortOrder = 1000)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        IsSystem = isSystem;
        SortOrder = sortOrder;
        CreatedAt = DateTimeOffset.UtcNow;
        Update(name, type, description, icon);
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

    public void ReplaceFieldDefinitions(IEnumerable<(string Key, string Label, AssetFieldType FieldType, string? Options, bool Required)> definitions)
    {
        FieldDefinitions.Clear();
        var sortOrder = 0;
        foreach (var definition in definitions)
        {
            FieldDefinitions.Add(new AssetFieldDefinition(Id, definition.Key, definition.Label, definition.FieldType, definition.Options, definition.Required, sortOrder));
            sortOrder++;
        }
    }
}
