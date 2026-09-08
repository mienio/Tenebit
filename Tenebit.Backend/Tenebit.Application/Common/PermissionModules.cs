namespace Tenebit.Application.Common;

// The full list of permissionable app areas. Each module gets a "view" RolePermissionKeys entry,
// and (when SupportsManage) a "manage" one too. This is the source PermissionService and the
// Settings > Permissions matrix both read from - see RolePermissionKeys.All, which is generated
// from this list.
public static class PermissionModules
{
    public const string Dashboard = "dashboard";
    public const string Assets = "assets";
    public const string CustomFields = "customFields";
    public const string Locations = "locations";
    public const string Licenses = "licenses";
    public const string Assignments = "assignments";
    public const string AssetAudits = "assetAudits";
    public const string People = "people";
    public const string Onboarding = "onboarding";
    public const string Offboarding = "offboarding";
    public const string Procedures = "procedures";
    public const string JobProfiles = "jobProfiles";
    public const string Reports = "reports";
    public const string ActivityLog = "activityLog";
    public const string Alerts = "alerts";
    public const string Settings = "settings";
    public const string OrganizationUsers = "organizationUsers";

    public static readonly IReadOnlyList<ModuleInfo> All = [
        new(Dashboard, "Pulpit", "Podsumowanie floty, kosztów i ostatniej aktywności.", SupportsManage: false),
        new(Assets, "Aktywa", "Sprzęt, kategorie, serwis, zgłoszenia i przeglądy.", SupportsManage: true),
        new(CustomFields, "Pola niestandardowe", "Definicje pól niestandardowych dla kategorii aktywów.", SupportsManage: true),
        new(Locations, "Lokalizacje", "Struktura lokalizacji i widok inwentarza.", SupportsManage: true),
        new(Licenses, "Licencje", "Licencje, przypisania miejsc i SaaS.", SupportsManage: true),
        new(Assignments, "Przypisania", "Wydawanie i zwroty sprzętu.", SupportsManage: true),
        new(AssetAudits, "Audyty aktywów", "Kampanie audytowe i weryfikacja pozycji.", SupportsManage: true),
        new(People, "Ludzie", "Pracownicy, zespoły i typy relacji.", SupportsManage: true),
        new(Onboarding, "Onboarding", "Pakiety wdrożeniowe nowych pracowników.", SupportsManage: true),
        new(Offboarding, "Offboarding", "Sprawy zakończenia współpracy.", SupportsManage: true),
        new(Procedures, "Procedury", "Zestawy procedur, wersje i publikacja.", SupportsManage: true),
        new(JobProfiles, "Stanowiska", "Profile stanowisk i przypisane procedury.", SupportsManage: true),
        new(Reports, "Raporty", "Eksporty i zestawienia.", SupportsManage: false),
        new(ActivityLog, "Log aktywności", "Historia zdarzeń w organizacji.", SupportsManage: false),
        new(Alerts, "Alerty", "Reguły powiadomień i podsumowania e-mail.", SupportsManage: true),
        new(Settings, "Ustawienia firmy", "Dane firmy, etykiety QR, statusy aktywów i prywatność dowodów.", SupportsManage: true),
        new(OrganizationUsers, "Użytkownicy", "Konta logowania członków organizacji.", SupportsManage: true)
    ];
}

public sealed record ModuleInfo(string Key, string Label, string Description, bool SupportsManage);

public static class PermissionActions
{
    public const string View = "view";
    public const string Manage = "manage";
}
