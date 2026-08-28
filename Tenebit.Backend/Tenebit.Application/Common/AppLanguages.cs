namespace Tenebit.Application.Common;

/// <summary>
/// Jedyne źródło prawdy o językach interfejsu. Wcześniej lista była powielona w
/// <see cref="ErrorMessageTranslator"/>, <see cref="EmailTemplates"/>, ProtocolLabels oraz w dwóch
/// miejscach czytających nagłówek <c>X-Ui-Language</c> - te ostatnie w ogóle nie sprawdzały, czy kod
/// jest obsługiwany, więc dowolna wartość nagłówka wędrowała dalej w głąb aplikacji.
///
/// Polski jest językiem źródłowym komunikatów, angielski pierwszym fallbackiem dla tłumaczeń.
/// </summary>
public static class AppLanguages
{
    public const string Source = "pl";
    public const string Fallback = "en";

    public static readonly IReadOnlyList<string> All = ["pl", "en", "es", "de", "it", "fr"];

    /// <summary>Sprowadza dowolne wejście (nagłówek HTTP, pole w profilu) do obsługiwanego kodu.
    /// Akceptuje też formy regionalne w rodzaju <c>it-CH</c> czy <c>fr_BE</c>.</summary>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return Source;

        var code = language.Trim().ToLowerInvariant();
        var separator = code.IndexOfAny(['-', '_']);
        if (separator > 0) code = code[..separator];

        return All.Contains(code) ? code : Source;
    }

    public static bool IsSupported(string? language) =>
        !string.IsNullOrWhiteSpace(language) && All.Contains(language.Trim().ToLowerInvariant());
}
