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
    [InlineData("it", "L'asset non esiste.")]
    [InlineData("fr", "L'actif n'existe pas.")]
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
        var result = ErrorMessageTranslator.Translate("Aktywo nie istnieje.", "cs");
        Assert.Equal("Aktywo nie istnieje.", result);
    }

    [Theory]
    [InlineData("it", "Limite di asset superato. Il piano Free consente 10 asset. Passa a un piano superiore.")]
    [InlineData("fr", "Limite d'actifs dépassée. Le forfait Free autorise 10 actifs. Passez à un forfait supérieur.")]
    public void Translate_TranslatesPlanLimitMessage_ForItalianAndFrench(string language, string expected)
    {
        var result = ErrorMessageTranslator.Translate("Limit aktywów przekroczony. Plan Free pozwala na 10 aktywów. Przejdź na wyższy plan.", language);
        Assert.Equal(expected, result);
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

    [Theory]
    [InlineData("Limit aktywów przekroczony. Plan Free pozwala na 10 aktywów. Przejdź na wyższy plan.", "Asset limit exceeded. The Free plan allows 10 assets. Upgrade your plan.")]
    [InlineData("Limit pracowników przekroczony. Plan Free pozwala na 10 pracowników. Przejdź na wyższy plan.", "Employee limit exceeded. The Free plan allows 10 employees. Upgrade your plan.")]
    [InlineData("Limit zestawów stanowiskowych przekroczony. Plan Free pozwala na 10 zestawów stanowiskowych. Przejdź na wyższy plan.", "Job profile limit exceeded. The Free plan allows 10 job profiles. Upgrade your plan.")]
    public void Translate_TranslatesEveryPlanLimitMessage(string message, string expected)
    {
        Assert.Equal(expected, ErrorMessageTranslator.Translate(message, "en"));
    }

    [Theory]
    [InlineData("Limit aktywów przekroczony. Plan Free pozwala na 10 aktywów. Przejdź na wyższy plan.", "SUBSCRIPTION_ASSET_LIMIT_EXCEEDED")]
    [InlineData("Limit licencji przekroczony. Plan Free pozwala na 10 licencji. Przejdź na wyższy plan.", "SUBSCRIPTION_RESOURCE_LIMIT_EXCEEDED")]
    [InlineData("Limit lokalizacji przekroczony. Plan Free pozwala na 10 lokalizacji. Przejdź na wyższy plan.", "SUBSCRIPTION_RESOURCE_LIMIT_EXCEEDED")]
    [InlineData("Limit procedur przekroczony. Plan Free pozwala na 10 procedur. Przejdź na wyższy plan.", "SUBSCRIPTION_RESOURCE_LIMIT_EXCEEDED")]
    [InlineData("Limit kategorii przekroczony. Plan Free pozwala na 10 kategorii. Przejdź na wyższy plan.", "SUBSCRIPTION_RESOURCE_LIMIT_EXCEEDED")]
    [InlineData("Limit planu Free (10) został osiągnięty dla pracowników, aktywów lub procedur. Przejdź na wyższy plan.", "SUBSCRIPTION_RESOURCE_LIMIT_EXCEEDED")]
    public void Resolve_GivesLimitMessagesAMachineReadableCode(string message, string expected)
    {
        Assert.Equal(expected, ErrorCodeResolver.Resolve(message));
    }

    // Komunikaty budowane z interpolacji ($"...") nie trafiaja do slownika Exact - lapie je dopiero
    // regex w Templates. Latwo dolozyc taki komunikat w serwisie i nie zauwazyc, ze zaden szablon go
    // nie obsluguje: aplikacja nie rzuca bledu, tylko po cichu pokazuje polski tekst obcojezycznemu
    // uzytkownikowi. Test bierze konkretne wystapienie kazdego takiego komunikatu i sprawdza, ze
    // realnie sie tlumaczy - samo dopasowanie regexa "na oko" to za malo.
    public static TheoryData<string> InterpolatedMessages() =>
    [
        "Przekroczono limit 50 akcji moderacyjnych na godzinę. To zabezpieczenie przed masowym działaniem z przejętego konta — odczekaj i spróbuj ponownie.",
        "Maksymalna liczba progów to 5.",
        "Próg musi być w zakresie 0–90 dni.",
        "Limit kategorii przekroczony. Plan Free pozwala na 10 kategorii. Przejdź na wyższy plan.",
        "Kategoria może mieć maksymalnie 200 pól własnych.",
        "Limit aktywów przekroczony. Plan Free pozwala na 10 aktywów. Przejdź na wyższy plan.",
        "Pole „Numer inwentarzowy” jest wymagane.",
        "Limit lokalizacji przekroczony. Plan Free pozwala na 10 lokalizacji. Przejdź na wyższy plan.",
        "Można przesłać maksymalnie 25 plików.",
        "Nieznana rola: superadmin.",
        "Limit zestawów stanowiskowych przekroczony. Plan Free pozwala na 10 zestawów stanowiskowych. Przejdź na wyższy plan.",
        "Limit licencji przekroczony. Plan Free pozwala na 10 licencji. Przejdź na wyższy plan.",
        "Limit planu Free (10) został osiągnięty dla pracowników, aktywów lub procedur. Przejdź na wyższy plan.",
        "Limit pracowników przekroczony. Plan Free pozwala na 10 pracowników. Przejdź na wyższy plan.",
        "Limit zespołów przekroczony. Plan Free pozwala na 10 zespołów. Przejdź na wyższy plan.",
        "Nazwa pliku może mieć maksymalnie 200 znaków.",
        "Limit procedur przekroczony. Plan Free pozwala na 10 procedur. Przejdź na wyższy plan.",
        "Nieznane uprawnienie: assets.superdelete.",
        "Nieznany status aktywa: Teleported.",
        "Aby przejść na plan Business, użyj płatności Stripe (checkout).",
    ];

    [Theory]
    [MemberData(nameof(InterpolatedMessages))]
    public void Translate_HandlesEveryInterpolatedMessage_ForEveryLanguage(string message)
    {
        foreach (var language in new[] { "en", "es", "de", "it", "fr" })
        {
            var result = ErrorMessageTranslator.Translate(message, language);
            Assert.False(string.IsNullOrWhiteSpace(result), $"{language}: pusty wynik");
            Assert.NotEqual(message, result);
        }
    }

    // Komunikaty z RequestObjectValidator (DataAnnotations) i z walidacji multipart. Wracaja do
    // klienta przez ValidationEndpointFilter, ktory biegnie przed KAZDYM handlerem Minimal API, wiec
    // luka tutaj dotyka calego API naraz. Nazwa pola jest techniczna i ma zostac nietknieta.
    public static TheoryData<string, string> ValidationMessages() =>
        new()
        {
            { "Pole Email może mieć maksymalnie 200 znaków.", "Email" },
            { "Pole AssetIds może zawierać maksymalnie 100 elementów.", "AssetIds" },
            { "Pole Name nie może być puste.", "Name" },
            { "Pole Email nie zawiera prawidłowego adresu e-mail.", "Email" },
            { "Pole ReturnUrl musi być względną ścieżką aplikacji.", "ReturnUrl" },
            { "Pole CategoryId musi zawierać prawidłowy identyfikator.", "CategoryId" },
            { "Pole StatusKey ma nieprawidłową wartość.", "StatusKey" },
            { "Pole Seats ma wartość poza dozwolonym zakresem.", "Seats" },
            { "Pole PurchasePrice nie może być ujemne.", "PurchasePrice" },
            { "Pole PurchaseDate ma datę poza dozwolonym zakresem.", "PurchaseDate" },
            { "Pole Notes zawiera nieprawidłową wartość tekstową.", "Notes" },
            { "Pole RetentionDays zawiera wartość poza dozwolonym zakresem 0-3650.", "RetentionDays" },
            { "Pole Items nie jest kolekcją.", "Items" },
            { "Klucz w polu CustomFields może mieć maksymalnie 80 znaków.", "CustomFields" },
            { "Wartość w polu CustomFields może mieć maksymalnie 500 znaków.", "CustomFields" },
            { "Pole 'request' jest wymagane.", "request" },
            { "Pole 'evidenceManifest' ma nieprawidłowy JSON.", "evidenceManifest" },
            { "Manifest może zawierać maksymalnie 50 pozycji.", "50" },
        };

    [Theory]
    [MemberData(nameof(ValidationMessages))]
    public void Translate_TranslatesValidationMessages_AndKeepsFieldName(string message, string fieldName)
    {
        foreach (var language in new[] { "en", "es", "de", "it", "fr" })
        {
            var result = ErrorMessageTranslator.Translate(message, language);
            Assert.NotEqual(message, result);
            Assert.Contains(fieldName, result);
            var leaked = result.Where(character => "ąćęłńśźżĄĆĘŁŃŚŹŻ".Contains(character)).ToArray();
            Assert.True(leaked.Length == 0, $"{language}: polskie znaki w tlumaczeniu -> {result}");
        }
    }

    // Kontrapunkt dla testu wyzej: wynik nie moze byc "przetlumaczony" tylko dlatego, ze szablon
    // przepisal polski tekst. Litery diakrytyczne wystepujace wylacznie w polskim alfabecie nie maja
    // prawa pojawic sie w zadnym z tlumaczen.
    [Theory]
    [MemberData(nameof(InterpolatedMessages))]
    public void Translate_LeavesNoPolishOnlyLetters_InInterpolatedMessages(string message)
    {
        foreach (var language in new[] { "en", "es", "de", "it", "fr" })
        {
            var result = ErrorMessageTranslator.Translate(message, language);
            var leaked = result.Where(character => "ąćęłńśźżĄĆĘŁŃŚŹŻ".Contains(character)).ToArray();
            Assert.True(leaked.Length == 0, $"{language}: polskie znaki w tlumaczeniu -> {result}");
        }
    }
}
