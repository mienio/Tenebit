using Tenebit.Domain.Common;

namespace Tenebit.Domain.Reservations;

public sealed class EquipmentKitDefinition
{
    private EquipmentKitDefinition() { }

    public EquipmentKitDefinition(Guid organizationId, string name, string? description, bool visibleInEmployeeCatalog, string createdBy, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa zestawu jest wymagana.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        VisibleInEmployeeCatalog = visibleInEmployeeCatalog;
        CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool VisibleInEmployeeCatalog { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public List<EquipmentKitDefinitionItem> Items { get; private set; } = [];

    public EquipmentKitDefinitionItem AddItem(Guid assetCategoryId, int requiredQuantity)
    {
        var item = new EquipmentKitDefinitionItem(OrganizationId, Id, assetCategoryId, requiredQuantity);
        Items.Add(item);
        return item;
    }
}
