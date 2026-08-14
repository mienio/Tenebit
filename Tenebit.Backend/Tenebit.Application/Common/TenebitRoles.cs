namespace Tenebit.Application.Common;

public static class TenebitRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string AssetOperator = "asset_operator";
    public const string Technician = "technician";
    public const string Manager = "manager";
    public const string Employee = "employee";
    public const string Hr = "hr";
    public const string ProcedureManager = "procedure_manager";
    public const string LicenseManager = "license_manager";
    public const string Finance = "finance";
    public const string Auditor = "auditor";

    public static readonly IReadOnlyList<RoleInfo> All = [
        new(Owner, "Właściciel", "Pełne uprawnienia do firmy, płatności, użytkowników i danych."),
        new(Admin, "Administrator", "Pełna konfiguracja operacyjna bez płatności właściciela."),
        new(AssetOperator, "Operator aktywów", "Dodaje aktywa, wydaje i przyjmuje sprzęt."),
        new(Technician, "Serwisant", "Wpisuje i aktualizuje aktywa, bez zarządzania ludźmi i rolami."),
        new(Manager, "Kierownik", "Widok zespołu, jego aktywów, procedur i akceptacji."),
        new(Employee, "Pracownik", "Widok tylko swoich aktywów, procedur i akceptacji."),
        new(Hr, "HR / onboarding", "Ludzie, stanowiska, pakiety i procedury pracownicze."),
        new(ProcedureManager, "Procedury", "Tworzy pliki procedur, wersje i przypisania do stanowisk."),
        new(LicenseManager, "Licencje", "Zarządza licencjami, SaaS i odnowieniami."),
        new(Finance, "Finanse", "Koszty, faktury, zakupy i raporty finansowe."),
        new(Auditor, "Audytor", "Tylko odczyt raportów, historii i logów.")
    ];

    // Read-access groups for the main data modules — kept in sync with each role's own write
    // permissions elsewhere (e.g. Technician/LicenseManager can already act on assets;
    // ProcedureManager can already author procedures) and with the frontend nav matrix
    // (components/Layout.tsx `nav`). "employee" is intentionally excluded from all of these —
    // its role is "view only own", served entirely through My Workspace.
    public static readonly string[] AssetViewers = [Owner, Admin, AssetOperator, Technician, Manager, Hr, LicenseManager, Finance, Auditor];
    public static readonly string[] PeopleViewers = [Owner, Admin, Manager, Hr, AssetOperator, Auditor];
    public static readonly string[] AssignmentViewers = [Owner, Admin, AssetOperator, Hr, Manager];
    public static readonly string[] ProcedureViewers = [Owner, Admin, Hr, Manager, AssetOperator, Auditor, ProcedureManager];
    public static readonly string[] LicenseViewers = [Owner, Admin, LicenseManager, Finance, Auditor];
    public static readonly string[] OffboardingManagers = [Owner, Admin, Hr, AssetOperator];
    public static readonly string[] AssetAuditViewers = [Owner, Admin, AssetOperator, Auditor];
    public static readonly string[] AssetAuditManagers = [Owner, Admin, AssetOperator];
    public static readonly string[] ReservationViewers = [Owner, Admin, AssetOperator];
}

public sealed record RoleInfo(string Key, string Label, string Description);
