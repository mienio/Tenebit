using System.Text.RegularExpressions;
using Tenebit.Domain.Common;

namespace Tenebit.Domain.Settings;

public sealed class AssetStatusSetting
{
    private static readonly Regex HexColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    private AssetStatusSetting() { }

    public AssetStatusSetting(Guid organizationId, string statusKey, string label, string color, string backgroundColor, int sortOrder, bool isEnabled)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        StatusKey = statusKey;
        Update(label, color, backgroundColor, sortOrder, isEnabled);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string StatusKey { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#1d4ed8";
    public string BackgroundColor { get; private set; } = "#eff6ff";
    public int SortOrder { get; private set; }
    public bool IsEnabled { get; private set; }

    public void Update(string label, string color, string backgroundColor, int sortOrder, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new DomainException("Etykieta statusu jest wymagana.");
        if (!HexColorPattern.IsMatch(color?.Trim() ?? string.Empty))
        {
            throw new DomainException("Kolor tekstu statusu musi być w formacie szesnastkowym, np. #1d4ed8.");
        }
        if (!HexColorPattern.IsMatch(backgroundColor?.Trim() ?? string.Empty))
        {
            throw new DomainException("Kolor tła statusu musi być w formacie szesnastkowym, np. #eff6ff.");
        }

        Label = label.Trim();
        Color = color!.Trim().ToLowerInvariant();
        BackgroundColor = backgroundColor!.Trim().ToLowerInvariant();
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
    }
}
