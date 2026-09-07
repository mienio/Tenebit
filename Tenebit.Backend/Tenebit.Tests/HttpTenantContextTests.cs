using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Tenebit.Api.Auth;

namespace Tenebit.Tests;

/// <summary>
/// Regression coverage for a production incident: an affiliate token attached to a request (e.g. a
/// stale in-memory token replayed against /api/partner/login) made HttpTenantContext.OrganizationId
/// throw instead of returning Guid.Empty, because only the platform-admin scope was exempted from the
/// "authenticated but no organization_id" guard. Affiliates are equally org-less by design and must be
/// exempted the same way - see AffiliateClaims.
/// </summary>
public sealed class HttpTenantContextTests
{
    private static HttpTenantContext BuildContext(ClaimsPrincipal? user)
    {
        var httpContext = new DefaultHttpContext();
        if (user is not null) httpContext.User = user;
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new HttpTenantContext(accessor);
    }

    private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    [Fact]
    public void Unauthenticated_request_returns_empty_organization_id()
    {
        var context = BuildContext(user: null);
        Assert.Equal(Guid.Empty, context.OrganizationId);
    }

    [Fact]
    public void Platform_admin_token_returns_empty_organization_id()
    {
        var user = AuthenticatedUser(new Claim(PlatformAdminClaims.ScopeClaimType, PlatformAdminClaims.ScopeValue));
        var context = BuildContext(user);
        Assert.Equal(Guid.Empty, context.OrganizationId);
    }

    [Fact]
    public void Affiliate_token_returns_empty_organization_id_instead_of_throwing()
    {
        var user = AuthenticatedUser(new Claim(AffiliateClaims.ScopeClaimType, AffiliateClaims.ScopeValue));
        var context = BuildContext(user);
        Assert.Equal(Guid.Empty, context.OrganizationId);
    }

    [Fact]
    public void Tenant_token_returns_its_organization_id()
    {
        var organizationId = Guid.NewGuid();
        var user = AuthenticatedUser(new Claim("organization_id", organizationId.ToString()));
        var context = BuildContext(user);
        Assert.Equal(organizationId, context.OrganizationId);
    }

    [Fact]
    public void Authenticated_request_with_no_recognized_scope_and_no_organization_id_throws()
    {
        var user = AuthenticatedUser(new Claim("sub", Guid.NewGuid().ToString()));
        var context = BuildContext(user);
        Assert.Throws<InvalidOperationException>(() => context.OrganizationId);
    }
}
