namespace Tenebit.Application.Common;

// Fine-grained, per-role toggles layered on top of the coarse role checks in AccessPolicy.
// Unlike TenebitRoles (which gates whole actions/endpoints), these gate individual sensitive
// fields — right now just the license key. Deliberately small and explicit rather than a full
// generic permission engine: add a new PermissionKey + a DefaultAllow row when the next field
// needs the same treatment, wire one HasPermissionAsync check at the point that returns it.
public static class RolePermissionKeys
{
    public const string ViewLicenseKeys = "licenses.viewKey";

    public static readonly IReadOnlyList<PermissionInfo> All = [
        new(ViewLicenseKeys, "Wyświetlanie kluczy licencyjnych", "Kto widzi pełny klucz/numer seryjny licencji zamiast zamaskowanego.")
    ];

    // Roles with no explicit override fall back to this default.
    public static readonly IReadOnlyDictionary<string, string[]> DefaultAllowedRoles = new Dictionary<string, string[]>
    {
        [ViewLicenseKeys] = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.LicenseManager]
    };
}

public sealed record PermissionInfo(string Key, string Label, string Description);
