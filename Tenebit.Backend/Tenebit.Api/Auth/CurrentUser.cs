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

    public Guid? PersonId
    {
        get
        {
            var fromClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue("person_id");
            return Guid.TryParse(fromClaim, out var personId) ? personId : null;
        }
    }

    public string Subject => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? string.Empty;

    public string Email => _httpContextAccessor.HttpContext?.User.FindFirstValue("email") ?? string.Empty;

    // Used to stamp tamper-evident confirmation records (assignment/procedure signing) with the
    // requester's IP. RemoteIpAddress is rewritten by UseForwardedHeaders (Program.cs) from the
    // single trusted proxy hop (nginx) — reading the X-Forwarded-For header directly here would let a
    // client forge the stamped IP by sending its own header value (audyt P1.3).
    public string IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

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
