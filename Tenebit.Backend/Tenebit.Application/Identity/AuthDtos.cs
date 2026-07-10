namespace Tenebit.Application.Identity;

public sealed record RegisterRequest(string OrganizationName, string Email, string Password, string DisplayName, string Currency, string? Language = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthUserResponse(Guid Id, Guid OrganizationId, string OrganizationName, string Email, string DisplayName, IReadOnlyList<string> Roles);

public sealed record ExternalUserInfo(string Provider, string ProviderUserId, string? Email, bool EmailVerified, string? DisplayName);
