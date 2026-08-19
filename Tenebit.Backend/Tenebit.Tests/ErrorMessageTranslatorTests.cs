using Tenebit.Application.Common;

namespace Tenebit.Tests;

public class ErrorMessageTranslatorTests
{
    [Fact]
    public void Translate_ReturnsOriginalMessage_WhenLanguageIsPolish()
    {
        var result = ErrorMessageTranslator.Translate("Aktywo nie istnieje.", "pl");
        Assert.Equal("Aktywo nie istnieje.", result);
    }

    [Theory]
    [InlineData("en", "The asset does not exist.")]
    [InlineData("es", "El activo no existe.")]
    [InlineData("de", "Das Asset existiert nicht.")]
    public void Translate_TranslatesExactMatchMessage_ForSupportedLanguages(string language, string expected)
    {
        var result = ErrorMessageTranslator.Translate("Aktywo nie istnieje.", language);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Translate_ReturnsOriginalMessage_WhenNoTranslationExists()
    {
        var result = ErrorMessageTranslator.Translate("Some message not in the dictionary.", "en");
        Assert.Equal("Some message not in the dictionary.", result);
    }

    [Fact]
    public void Translate_ReturnsOriginalMessage_WhenLanguageIsUnsupported()
    {
        var result = ErrorMessageTranslator.Translate("Aktywo nie istnieje.", "fr");
        Assert.Equal("Aktywo nie istnieje.", result);
    }

    [Fact]
    public void Translate_ReturnsOriginalMessage_WhenLanguageIsNull()
    {
        var result = ErrorMessageTranslator.Translate("Aktywo nie istnieje.", null);
        Assert.Equal("Aktywo nie istnieje.", result);
    }

    [Theory]
    [InlineData("en", "Asset limit exceeded. The Pro plan allows 100 assets. Upgrade your plan.")]
    [InlineData("es", "Límite de activos superado. El plan Pro permite 100 activos. Actualiza tu plan.")]
    [InlineData("de", "Asset-Limit überschritten. Der Plan Pro erlaubt 100 Assets. Aktualisieren Sie Ihren Plan.")]
    public void Translate_TranslatesTemplatedMessage_PreservingRuntimeValues(string language, string expected)
    {
        var message = "Limit aktywów przekroczony. Plan Pro pozwala na 100 aktywów. Przejdź na wyższy plan.";
        var result = ErrorMessageTranslator.Translate(message, language);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Translate_TranslatesUnknownRoleTemplate()
    {
        var result = ErrorMessageTranslator.Translate("Nieznana rola: superadmin.", "en");
        Assert.Equal("Unknown role: superadmin.", result);
    }

    [Fact]
    public void Translate_TranslatesRequiredFieldTemplate_WithCurlyPolishQuotes()
    {
        var result = ErrorMessageTranslator.Translate("Pole „Numer seryjny” jest wymagane.", "en");
        Assert.Equal("The field \"Numer seryjny\" is required.", result);
    }

    [Theory]
    [InlineData("pl", "Nieznany plan: gold")]
    [InlineData("es", "Plan desconocido: gold")]
    [InlineData("de", "Unbekannter Plan: gold")]
    public void Translate_TranslatesEnglishSourcedUnknownPlanMessage_ForNonEnglishLanguages(string language, string expected)
    {
        // "Unknown plan: {planKey}" is itself an English string in the otherwise Polish-default
        // backend (a reverse-leak bug); it must still be translated for pl/es/de and left as-is for en.
        var result = ErrorMessageTranslator.Translate("Unknown plan: gold", language);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Translate_LeavesUnknownPlanMessageUnchanged_ForEnglish()
    {
        var result = ErrorMessageTranslator.Translate("Unknown plan: gold", "en");
        Assert.Equal("Unknown plan: gold", result);
    }

    [Theory]
    [InlineData("Akceptacja regulaminu i polityki prywatności jest wymagana.", "en", "Acceptance of the terms and privacy policy is required.")]
    [InlineData("Akceptacja regulaminu i polityki prywatności jest wymagana.", "es", "Debes aceptar los términos y la política de privacidad.")]
    [InlineData("Akceptacja regulaminu i polityki prywatności jest wymagana.", "de", "Die Nutzungsbedingungen und die Datenschutzerklärung müssen akzeptiert werden.")]
    [InlineData("Zbyt wiele prób. Poproś o nowy kod lub spróbuj ponownie później.", "en", "Too many attempts. Request a new code or try again later.")]
    [InlineData("Zbyt wiele prób. Poproś o nowy kod lub spróbuj ponownie później.", "es", "Demasiados intentos. Solicita un código nuevo o inténtalo de nuevo más tarde.")]
    [InlineData("Zbyt wiele prób. Poproś o nowy kod lub spróbuj ponownie później.", "de", "Zu viele Versuche. Fordern Sie einen neuen Code an oder versuchen Sie es später erneut.")]
    public void Translate_TranslatesRecoveryAndLegalMessages(string message, string language, string expected)
    {
        var result = ErrorMessageTranslator.Translate(message, language);
        Assert.Equal(expected, result);
    }
}
