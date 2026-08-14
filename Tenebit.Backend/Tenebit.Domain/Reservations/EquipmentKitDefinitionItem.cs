using Tenebit.Domain.Common;

namespace Tenebit.Domain.Reservations;

public sealed class EquipmentKitDefinitionItem
{
    private EquipmentKitDefinitionItem() { }

    public EquipmentKitDefinitionItem(Guid organizationId, Guid kitDefinitionId, Guid assetCategoryId, int requiredQuantity)
    {
        if (requiredQuantity <= 0)
        {
            throw new DomainException("Wymagana ilość musi być większa od zera.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        KitDefinitionId = kitDefinitionId;
        AssetCategoryId = assetCategoryId;
        RequiredQuantity = requiredQuantity;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid KitDefinitionId { get; private set; }
    public Guid AssetCategoryId { get; private set; }
    public int RequiredQuantity { get; private set; }
}
