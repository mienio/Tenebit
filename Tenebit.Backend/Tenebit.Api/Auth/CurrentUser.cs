using System.Security.Claims;
using Tenebit.Application.Abstractions;

namespace Tenebit.Api.Auth;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid OrganizationId
    {
        get
        {
            var fromClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue("organization_id");
            return Guid.TryParse(fromClaim, out var organizationId) ? organizationId : Guid.Empty;
        }
    }

    public string Subject => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? string.Empty;

    public string Email => _httpContextAccessor.HttpContext?.User.FindFirstValue("email") ?? string.Empty;

    // Used to stamp tamper-evident confirmation records (assignment/procedure signing) with the
    // requester's IP — checked ahead of RemoteIpAddress so a proxied deployment (Docker/nginx) still
    // records the real client address instead of the proxy's.
    public string IpAddress
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null) return string.Empty;
            var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();
            return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }
    }

    public string Language
    {
        get
        {
            var header = _httpContextAccessor.HttpContext?.Request.Headers["X-Ui-Language"].ToString();
            return string.IsNullOrWhiteSpace(header) ? "pl" : header.Trim().ToLowerInvariant();
        }
    }

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null) return Array.Empty<string>();
            return user.Claims
                .Where(claim => claim.Type is ClaimTypes.Role or "role" or "roles" or "groups")
                .SelectMany(claim => claim.Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
