namespace Tenebit.Domain.Assets;

public sealed class AssetFieldValue
{
    private AssetFieldValue() { }

    public AssetFieldValue(Guid assetId, string fieldKey, string value)
    {
        AssetId = assetId;
        FieldKey = fieldKey;
        Value = value;
    }

    public Guid AssetId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
}
