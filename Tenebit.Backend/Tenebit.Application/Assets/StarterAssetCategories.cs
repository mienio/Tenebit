using Tenebit.Domain.Assets;

namespace Tenebit.Application.Assets;

public static class StarterAssetCategories
{
    private static readonly (string Name, AssetCategoryType Type, string Description, string Icon)[] Definitions =
    [
        ("Laptopy", AssetCategoryType.Physical, "Komputery przenośne i stacje robocze.", "laptop"),
        ("Monitory", AssetCategoryType.Physical, "Monitory, ekrany i projektory.", "monitor"),
        ("Klawiatury", AssetCategoryType.Physical, "Klawiatury przewodowe i bezprzewodowe.", "keyboard"),
        ("Myszy", AssetCategoryType.Physical, "Myszy i trackpady.", "mouse"),
        ("Telefony", AssetCategoryType.Physical, "Telefony firmowe i urządzenia mobilne.", "smartphone"),
        ("Słuchawki", AssetCategoryType.Physical, "Słuchawki i zestawy słuchawkowe.", "headphones"),
        ("Stacje dokujące", AssetCategoryType.Physical, "Docki i huby USB-C.", "plug-zap"),
        ("Tablety", AssetCategoryType.Physical, "Tablety i czytniki.", "tablet"),
        ("Drukarki", AssetCategoryType.Physical, "Drukarki biurowe i etykietujące.", "printer"),
        ("Ładowarki i zasilacze", AssetCategoryType.Physical, "Zasilacze, ładowarki i powerbanki.", "battery-charging"),
        ("Kable i adaptery", AssetCategoryType.Physical, "Kable, przejściówki i adaptery.", "cable"),
        ("Nośniki USB", AssetCategoryType.Physical, "Pendrive'y i nośniki przenośne.", "usb"),
        ("Dyski zewnętrzne", AssetCategoryType.Physical, "Dyski zewnętrzne i macierze.", "hard-drive"),
        ("Kamery internetowe", AssetCategoryType.Physical, "Kamery internetowe i do wideokonferencji.", "webcam"),
        ("Routery", AssetCategoryType.Physical, "Routery i modemy.", "router"),
        ("Switche sieciowe", AssetCategoryType.Physical, "Przełączniki i infrastruktura sieciowa.", "network"),
        ("Punkty dostępowe Wi-Fi", AssetCategoryType.Physical, "Access pointy i wzmacniacze sygnału.", "wifi"),
        ("Serwery", AssetCategoryType.Physical, "Serwery i macierze serwerowe.", "server"),
        ("Zasilacze UPS", AssetCategoryType.Physical, "Zasilacze awaryjne i listwy.", "zap"),
        ("Projektory", AssetCategoryType.Physical, "Projektory i ekrany do prezentacji.", "projector"),
        ("Telewizory / ekrany", AssetCategoryType.Physical, "Telewizory i ekrany informacyjne.", "tv"),
        ("Sprzęt konferencyjny", AssetCategoryType.Physical, "Zestawy do sal konferencyjnych.", "presentation"),
        ("Aparaty fotograficzne", AssetCategoryType.Physical, "Aparaty i sprzęt foto.", "camera"),
        ("Kamery monitoringu", AssetCategoryType.Physical, "Kamery CCTV i monitoring.", "video"),
        ("Mikrofony", AssetCategoryType.Physical, "Mikrofony i sprzęt nagraniowy.", "mic"),
        ("Głośniki", AssetCategoryType.Physical, "Głośniki i systemy audio.", "speaker"),
        ("Skanery", AssetCategoryType.Physical, "Skanery dokumentów.", "scan-line"),
        ("Czytniki kodów kreskowych", AssetCategoryType.Physical, "Skanery kodów i terminale.", "barcode"),
        ("Krzesła biurowe", AssetCategoryType.Physical, "Krzesła i fotele biurowe.", "armchair"),
        ("Oświetlenie biurowe", AssetCategoryType.Physical, "Lampki i oświetlenie stanowiskowe.", "lamp"),
        ("Torby i plecaki", AssetCategoryType.Physical, "Torby, plecaki i etui na sprzęt.", "briefcase"),
        ("Zegarki", AssetCategoryType.Physical, "Smartwatche i opaski.", "watch"),
        ("Narzędzia", AssetCategoryType.Physical, "Narzędzia serwisowe i warsztatowe.", "wrench"),
        ("Sprzęt BHP", AssetCategoryType.Physical, "Odzież i sprzęt ochronny.", "hard-hat"),
        ("Okulary ochronne", AssetCategoryType.Physical, "Okulary i akcesoria ochronne.", "glasses"),
        ("Urządzenia IoT", AssetCategoryType.Physical, "Czujniki i urządzenia smart.", "antenna"),
        ("Karty SIM", AssetCategoryType.Digital, "Karty SIM i numery telefoniczne.", "credit-card"),
        ("Domeny internetowe", AssetCategoryType.Digital, "Domeny i adresy internetowe.", "globe"),
        ("Karty dostępu", AssetCategoryType.Key, "Karty zbliżeniowe i przepustki.", "id-card"),
        ("Klucze i dostęp", AssetCategoryType.Key, "Klucze fizyczne i depozyty.", "key"),
        ("Identyfikatory", AssetCategoryType.Key, "Identyfikatory pracownicze.", "badge"),
        ("Pojazdy", AssetCategoryType.Vehicle, "Samochody służbowe i floty.", "car"),
        ("Karty paliwowe", AssetCategoryType.Vehicle, "Karty paliwowe i flotowe.", "fuel"),
        ("Karty parkingowe", AssetCategoryType.Vehicle, "Abonamenty i karty parkingowe.", "parking-square"),
        ("Licencje oprogramowania", AssetCategoryType.License, "Licencje i klucze aktywacyjne.", "shield"),
        ("Konta SaaS", AssetCategoryType.Account, "Konta w usługach chmurowych.", "cloud"),
        ("Konta e-mail", AssetCategoryType.Account, "Skrzynki i konta pocztowe.", "mail"),
        ("Subskrypcje", AssetCategoryType.Account, "Subskrypcje i usługi cykliczne.", "receipt"),
        ("Certyfikaty", AssetCategoryType.Document, "Certyfikaty i uprawnienia.", "file-signature"),
        ("Umowy", AssetCategoryType.Document, "Umowy najmu i powierzenia sprzętu.", "scroll-text"),
        ("Ubezpieczenia", AssetCategoryType.Document, "Polisy i ubezpieczenia sprzętu.", "shield-check"),
        ("Materiały eksploatacyjne", AssetCategoryType.Consumable, "Tonery, baterie i materiały zużywalne.", "box")
    ];

    private static readonly HashSet<string> InspectionRequiredNames = ["Laptopy", "Telefony", "Pojazdy"];

    public static IReadOnlyList<AssetCategory> Create(Guid organizationId)
    {
        var categories = new List<AssetCategory>(Definitions.Length);
        for (var i = 0; i < Definitions.Length; i++)
        {
            var (name, type, description, icon) = Definitions[i];
            var returnHandlingMode = InspectionRequiredNames.Contains(name) ? ReturnHandlingMode.InspectionRequired : ReturnHandlingMode.DirectToStock;
            categories.Add(new AssetCategory(organizationId, name, type, description, icon, isSystem: true, sortOrder: i + 1, returnHandlingMode: returnHandlingMode, postReturnDisposition: PostReturnDisposition.Reuse));
        }
        return categories;
    }
}
