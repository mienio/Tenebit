namespace Tenebit.Application.Common;

// Per-(role, permission) toggles, org-overridable via role_permissions (see RolePermissionService).
// Two kinds of entries live here:
//  - one "{module}.view" / "{module}.manage" pair per PermissionModules entry, checked by
//    PermissionService and replacing the old hard-coded AccessPolicy.EnsureAnyRole role arrays;
//  - ViewLicenseKeys, a narrower field-level permission (masking a secret value) that sits
//    alongside module access rather than gating it.
public static class RolePermissionKeys
{
    public const string ViewLicenseKeys = "licenses.viewKey";

    public static readonly IReadOnlyList<PermissionInfo> All = BuildCatalog();

    public static readonly IReadOnlyDictionary<string, string[]> DefaultAllowedRoles = BuildDefaults();

    private static string ViewKey(string module) => $"{module}.view";
    private static string ManageKey(string module) => $"{module}.manage";

    private static IReadOnlyList<PermissionInfo> BuildCatalog()
    {
        var entries = new List<PermissionInfo>();
        foreach (var module in PermissionModules.All)
        {
            entries.Add(new PermissionInfo(ViewKey(module.Key), $"{module.Label} - podgląd", $"Kto widzi moduł „{module.Label}”."));
            if (module.SupportsManage)
            {
                entries.Add(new PermissionInfo(ManageKey(module.Key), $"{module.Label} - zarządzanie", $"Kto może dodawać, edytować i usuwać w module „{module.Label}”."));
            }
        }

        entries.Add(new PermissionInfo(ViewLicenseKeys, "Wyświetlanie kluczy licencyjnych", "Kto widzi pełny klucz/numer seryjny licencji zamiast zamaskowanego."));
        return entries;
    }

    private static IReadOnlyDictionary<string, string[]> BuildDefaults()
    {
        var allRoleKeys = TenebitRoles.All.Select(x => x.Key).ToArray();

        return new Dictionary<string, string[]>
        {
            [ViewKey(PermissionModules.Dashboard)] = TenebitRoles.DashboardViewers,

            [ViewKey(PermissionModules.Assets)] = TenebitRoles.AssetViewers,
            [ManageKey(PermissionModules.Assets)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator],

            [ViewKey(PermissionModules.CustomFields)] = TenebitRoles.AssetViewers,
            [ManageKey(PermissionModules.CustomFields)] = [TenebitRoles.Owner, TenebitRoles.Admin],

            [ViewKey(PermissionModules.Locations)] = TenebitRoles.LocationInventoryViewers,
            [ManageKey(PermissionModules.Locations)] = [TenebitRoles.Owner, TenebitRoles.Admin],

            [ViewKey(PermissionModules.Licenses)] = TenebitRoles.LicenseViewers,
            [ManageKey(PermissionModules.Licenses)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.LicenseManager],

            [ViewKey(PermissionModules.Assignments)] = TenebitRoles.AssignmentViewers,
            [ManageKey(PermissionModules.Assignments)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Hr, TenebitRoles.Technician],

            [ViewKey(PermissionModules.AssetAudits)] = TenebitRoles.AssetAuditViewers,
            [ManageKey(PermissionModules.AssetAudits)] = TenebitRoles.AssetAuditManagers,

            [ViewKey(PermissionModules.People)] = TenebitRoles.PeopleViewers,
            [ManageKey(PermissionModules.People)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr],

            [ViewKey(PermissionModules.Onboarding)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator],
            [ManageKey(PermissionModules.Onboarding)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.AssetOperator],

            [ViewKey(PermissionModules.Offboarding)] = TenebitRoles.OffboardingManagers,
            [ManageKey(PermissionModules.Offboarding)] = TenebitRoles.OffboardingManagers,

            [ViewKey(PermissionModules.Procedures)] = TenebitRoles.ProcedureViewers,
            [ManageKey(PermissionModules.Procedures)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.ProcedureManager],

            // JobProfiles listing has no role gate today - keep it open to every role by default.
            [ViewKey(PermissionModules.JobProfiles)] = allRoleKeys,
            [ManageKey(PermissionModules.JobProfiles)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Hr, TenebitRoles.ProcedureManager],

            [ViewKey(PermissionModules.Reports)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Manager, TenebitRoles.Finance, TenebitRoles.Auditor, TenebitRoles.AssetOperator],

            [ViewKey(PermissionModules.ActivityLog)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Auditor, TenebitRoles.AssetOperator, TenebitRoles.Manager, TenebitRoles.Hr],

            [ViewKey(PermissionModules.Alerts)] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Auditor],
            [ManageKey(PermissionModules.Alerts)] = [TenebitRoles.Owner, TenebitRoles.Admin],

            [ViewKey(PermissionModules.Settings)] = [TenebitRoles.Owner, TenebitRoles.Admin],
            [ManageKey(PermissionModules.Settings)] = [TenebitRoles.Owner, TenebitRoles.Admin],

            [ViewKey(PermissionModules.OrganizationUsers)] = [TenebitRoles.Owner, TenebitRoles.Admin],
            [ManageKey(PermissionModules.OrganizationUsers)] = [TenebitRoles.Owner, TenebitRoles.Admin],

            [ViewLicenseKeys] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.LicenseManager]
        };
    }
}

public sealed record PermissionInfo(string Key, string Label, string Description);
