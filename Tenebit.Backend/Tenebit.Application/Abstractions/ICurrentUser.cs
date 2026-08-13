namespace Tenebit.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid OrganizationId { get; }
    string Subject { get; }
    string Email { get; }
    string Language { get; }
    string IpAddress { get; }
    IReadOnlyCollection<string> Roles { get; }

    bool HasAnyRole(params string[] roles) => roles.Any(role => Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
}
