using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assets;

public sealed class AssetFieldDefinition
{
    private AssetFieldDefinition() { }

    public AssetFieldDefinition(Guid categoryId, string key, string label, AssetFieldType fieldType, string? options, bool required, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainException("Klucz pola własnego jest wymagany.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("Etykieta pola własnego jest wymagana.");
        }

        Id = Guid.NewGuid();
        CategoryId = categoryId;
        Key = key.Trim();
        Label = label.Trim();
        FieldType = fieldType;
        Options = string.IsNullOrWhiteSpace(options) ? null : options.Trim();
        Required = required;
        SortOrder = sortOrder;
    }

    public Guid CategoryId { get; private set; }
    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public AssetFieldType FieldType { get; private set; }
    public string? Options { get; private set; }
    public bool Required { get; private set; }
    public int SortOrder { get; private set; }

    public IReadOnlyList<string> OptionList => string.IsNullOrWhiteSpace(Options)
        ? []
        : Options.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Aktualizacja w miejscu (bez zmiany Id) - pozwala AssetCategory.ReplaceFieldDefinitions robić
    // diff zamiast Clear()+Add(), żeby niezmienione pola nie generowały DELETE+INSERT przy każdym zapisie.
    internal void UpdateDetails(string label, AssetFieldType fieldType, string? options, bool required, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("Etykieta pola własnego jest wymagana.");
        }

        Label = label.Trim();
        FieldType = fieldType;
        Options = string.IsNullOrWhiteSpace(options) ? null : options.Trim();
        Required = required;
        SortOrder = sortOrder;
    }
}
