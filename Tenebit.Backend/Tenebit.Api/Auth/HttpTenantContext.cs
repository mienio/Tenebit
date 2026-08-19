using System.Security.Claims;
using Tenebit.Application.Abstractions;

namespace Tenebit.Api.Auth;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public Guid OrganizationId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue("organization_id");
            return Guid.TryParse(value, out var organizationId) ? organizationId : Guid.Empty;
        }
    }
}
