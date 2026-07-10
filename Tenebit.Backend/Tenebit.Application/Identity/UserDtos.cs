using Tenebit.Application.Common;

namespace Tenebit.Application.Identity;

public sealed record OrganizationUserResponse(Guid Id, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles, DateTimeOffset CreatedAt);
public sealed record SaveOrganizationUserRequest(string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles);
public sealed record RoleResponse(string Key, string Label, string Description);
