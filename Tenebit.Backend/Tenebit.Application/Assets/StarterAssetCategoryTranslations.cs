namespace Tenebit.Application.Assets;

public static class StarterAssetCategoryTranslations
{
    public static readonly IReadOnlyDictionary<string, (string Name, string Description)> EnglishByPolishName = new Dictionary<string, (string Name, string Description)>
    {
        ["Laptopy"] = ("Laptops", "Portable computers and workstations."),
        ["Monitory"] = ("Monitors", "Monitors, screens and projectors."),
        ["Klawiatury"] = ("Keyboards", "Wired and wireless keyboards."),
        ["Myszy"] = ("Mice", "Mice and trackpads."),
        ["Telefony"] = ("Phones", "Company phones and mobile devices."),
        ["Słuchawki"] = ("Headsets", "Headphones and headsets."),
        ["Stacje dokujące"] = ("Docking stations", "Docks and USB-C hubs."),
        ["Tablety"] = ("Tablets", "Tablets and e-readers."),
        ["Drukarki"] = ("Printers", "Office and label printers."),
        ["Ładowarki i zasilacze"] = ("Chargers and power supplies", "Power supplies, chargers and power banks."),
        ["Kable i adaptery"] = ("Cables and adapters", "Cables, converters and adapters."),
        ["Nośniki USB"] = ("USB drives", "USB flash drives and portable media."),
        ["Dyski zewnętrzne"] = ("External drives", "External drives and storage arrays."),
        ["Kamery internetowe"] = ("Webcams", "Webcams and video conferencing cameras."),
        ["Routery"] = ("Routers", "Routers and modems."),
        ["Switche sieciowe"] = ("Network switches", "Switches and network infrastructure."),
        ["Punkty dostępowe Wi-Fi"] = ("Wi-Fi access points", "Access points and signal boosters."),
        ["Serwery"] = ("Servers", "Servers and server arrays."),
        ["Zasilacze UPS"] = ("UPS units", "Backup power supplies and power strips."),
        ["Projektory"] = ("Projectors", "Projectors and presentation screens."),
        ["Telewizory / ekrany"] = ("TVs / screens", "TVs and information displays."),
        ["Sprzęt konferencyjny"] = ("Conference equipment", "Conference room equipment sets."),
        ["Aparaty fotograficzne"] = ("Cameras", "Cameras and photo equipment."),
        ["Kamery monitoringu"] = ("Surveillance cameras", "CCTV cameras and monitoring."),
        ["Mikrofony"] = ("Microphones", "Microphones and recording equipment."),
        ["Głośniki"] = ("Speakers", "Speakers and audio systems."),
        ["Skanery"] = ("Scanners", "Document scanners."),
        ["Czytniki kodów kreskowych"] = ("Barcode readers", "Barcode scanners and terminals."),
        ["Krzesła biurowe"] = ("Office chairs", "Office chairs and armchairs."),
        ["Oświetlenie biurowe"] = ("Office lighting", "Desk lamps and workstation lighting."),
        ["Torby i plecaki"] = ("Bags and backpacks", "Bags, backpacks and equipment cases."),
        ["Zegarki"] = ("Watches", "Smartwatches and fitness bands."),
        ["Narzędzia"] = ("Tools", "Service and workshop tools."),
        ["Sprzęt BHP"] = ("Safety equipment", "Protective clothing and equipment."),
        ["Okulary ochronne"] = ("Safety glasses", "Protective glasses and accessories."),
        ["Urządzenia IoT"] = ("IoT devices", "Sensors and smart devices."),
        ["Karty SIM"] = ("SIM cards", "SIM cards and phone numbers."),
        ["Domeny internetowe"] = ("Internet domains", "Domains and web addresses."),
        ["Karty dostępu"] = ("Access cards", "Proximity cards and passes."),
        ["Klucze i dostęp"] = ("Keys and access", "Physical keys and deposits."),
        ["Identyfikatory"] = ("ID badges", "Employee ID badges."),
        ["Pojazdy"] = ("Vehicles", "Company cars and fleets."),
        ["Karty paliwowe"] = ("Fuel cards", "Fuel and fleet cards."),
        ["Karty parkingowe"] = ("Parking cards", "Parking subscriptions and cards."),
        ["Licencje oprogramowania"] = ("Software licenses", "Licenses and activation keys."),
        ["Konta SaaS"] = ("SaaS accounts", "Accounts in cloud services."),
        ["Konta e-mail"] = ("Email accounts", "Mailboxes and email accounts."),
        ["Subskrypcje"] = ("Subscriptions", "Subscriptions and recurring services."),
        ["Certyfikaty"] = ("Certificates", "Certificates and qualifications."),
        ["Umowy"] = ("Contracts", "Rental and equipment assignment contracts."),
        ["Ubezpieczenia"] = ("Insurance", "Insurance policies for equipment."),
        ["Materiały eksploatacyjne"] = ("Consumables", "Toners, batteries and consumable supplies.")
    };

    public static string TranslateName(bool isSystem, string language, string name)
    {
        if (isSystem && language != "pl" && EnglishByPolishName.TryGetValue(name, out var translation))
        {
            return translation.Name;
        }

        return name;
    }
}
