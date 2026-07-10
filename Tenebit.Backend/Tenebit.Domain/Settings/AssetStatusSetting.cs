using Tenebit.Domain.Common;

namespace Tenebit.Domain.Settings;

public sealed class AssetStatusSetting
{
    private AssetStatusSetting() { }

    public AssetStatusSetting(Guid organizationId, string statusKey, string label, int sortOrder, bool isEnabled)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        StatusKey = statusKey;
        Update(label, sortOrder, isEnabled);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string StatusKey { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsEnabled { get; private set; }

    public void Update(string label, int sortOrder, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new DomainException("Etykieta statusu jest wymagana.");
        Label = label.Trim();
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
    }
}
