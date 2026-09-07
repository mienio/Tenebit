namespace Tenebit.Api.Auth;

public static class AffiliateHttpContextExtensions
{
    /// <summary>Reads the affiliate id straight off the validated JWT `sub` claim - every
    /// /api/partner/* route (besides the anonymous auth ones) already requires the "Affiliate" policy,
    /// so this claim is guaranteed present and Program.cs's OnTokenValidated already re-verified it
    /// against a live, non-blocked affiliate before the request got this far.</summary>
    public static Guid GetAffiliateId(this HttpContext http) =>
        Guid.Parse(http.User.FindFirst("sub")!.Value);
}
