namespace Tenebit.Application.Settings;

public sealed record RolePermissionResponse(string RoleKey, string RoleLabel, string PermissionKey, string PermissionLabel, string PermissionDescription, bool Allowed);
public sealed record SetRolePermissionRequest(string RoleKey, string PermissionKey, bool Allowed);
