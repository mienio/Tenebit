namespace Tenebit.Api.Auth;

/// <summary>Third, fully isolated token scope alongside <see cref="PlatformAdminClaims"/> (platform
/// admin) and the tenant scope (organization_id-bearing tokens with no explicit scope claim at all).
/// An affiliate token carries no organization_id and no PlatformAdmin claim - see
/// TenebitEndpoints/AdminEndpoints for the explicit deny-list that rejects this scope on every tenant
/// and admin route, and PartnerEndpoints for the equivalent rejection of the other two scopes.</summary>
public static class AffiliateClaims
{
    public const string ScopeClaimType = "token_scope";
    public const string ScopeValue = "affiliate";
}
